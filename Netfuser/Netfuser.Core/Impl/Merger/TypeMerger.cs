using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Netfuser.Core.Impl.Merger
{
    class TypeMerger : BaseMerger
    {
        private readonly TypeMapping _tm;
        private readonly bool _merge;

        private readonly List<Tuple<MethodDef, MethodDef>> _methods;
        private readonly List<Tuple<FieldDef, FieldDef>> _fields;
        private readonly List<Tuple<PropertyDef, PropertyDef>> _properties;
        private readonly List<Tuple<EventDef, EventDef>> _events;

        private readonly List<MethodDef> _checkAdjustFamorassemMethods;

        private TypeMerger(ContextImpl context, Importer importer, TypeMapping tm, bool merge)
            : base(context, importer)
        {
            _tm = tm;
            _merge = merge;
            _fields = new List<Tuple<FieldDef, FieldDef>>();
            _methods = new List<Tuple<MethodDef, MethodDef>>();
            _properties = new List<Tuple<PropertyDef, PropertyDef>>();
            _events = new List<Tuple<EventDef, EventDef>>();
            _checkAdjustFamorassemMethods = new List<MethodDef>();
        }

        GenericParamConstraintUser Clone(GenericParamConstraint source)
        {
            var result = new GenericParamConstraintUser(Importer.Import(source.Constraint));
            CopyCustomAttributes(source, result);
            CopyCustomDebugInfo(source, result);
            return result;
        }

        void CopyGenericParameters(ITypeOrMethodDef source, ITypeOrMethodDef dest)
        {
            foreach (var gp in source.GenericParameters)
            {
                var target = Context.Fire(new NetfuserEvent.CreateGenericParameter(Context, gp, dest, _tm)
                    {Target = new GenericParamUser(gp.Number, gp.Flags, gp.Name)}).Target;
                dest.GenericParameters.Add(target);
                foreach (var c in gp.GenericParamConstraints)
                    target.GenericParamConstraints.Add(Clone(c));
            }
        }

        bool Equals(IList<GenericParam> a, IList<GenericParam> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
            {
                var pa = a[i];
                var pb = b[i];
                if (pa.Flags != pb.Flags) return false;
                if (pa.GenericParamConstraints.Count != pb.GenericParamConstraints.Count) return false;
                for (var j = 0; j < pa.GenericParamConstraints.Count; j++)
                    if (!new SigComparer().Equals(pa.GenericParamConstraints[j].Constraint.ToTypeSig(),
                        pb.GenericParamConstraints[j].Constraint.ToTypeSig()))
                        return false;
            }

            return true;
        }

        void CopyImplMap(IMemberForwarded source, IMemberForwarded dest)
        {
            if (source.HasImplMap)
                dest.ImplMap = new ImplMapUser(source.ImplMap.Module, source.ImplMap.Name, source.ImplMap.Attributes);
        }

        void CopyMarshalType(IHasFieldMarshal source, IHasFieldMarshal dest)
        {
            if (!source.HasMarshalType) return;
            switch (source.MarshalType)
            {
                case SafeArrayMarshalType s:
                    dest.MarshalType = new SafeArrayMarshalType(s.VariantType, Importer.Import(s.UserDefinedSubType));
                    break;
                case CustomMarshalType s:
                    dest.MarshalType = new CustomMarshalType(s.Guid, s.NativeTypeName,
                        Importer.Import(s.CustomMarshaler), s.Cookie);
                    break;
                default:
                    dest.MarshalType = source.MarshalType;
                    break;
            }
        }

        InterfaceImpl Clone(InterfaceImpl source)
        {
            var result = new InterfaceImplUser(Importer.Import(source.Interface));
            CopyCustomAttributes(source, result);
            CopyCustomDebugInfo(source, result);
            return result;
        }

        Constant Clone(Constant source) => new ConstantUser(source.Value, source.Type);

        void Copy(FieldDef source, FieldDef result)
        {
            CopyMarshalType(source, result);
            if (source.InitialValue != null)
                result.InitialValue = source.InitialValue;
            CopyImplMap(source, result);
            if (source.HasConstant)
                result.Constant = Clone(source.Constant);
            CopyCustomAttributes(source, result);
            CopyCustomDebugInfo(source, result);
        }

        void Copy(PropertyDef source, PropertyDef result)
        {
            foreach (var sm in source.GetMethods)
                result.GetMethods.Add(Importer.Import(sm).ResolveMethodDef());
            foreach (var sm in source.SetMethods)
                result.SetMethods.Add(Importer.Import(sm).ResolveMethodDef());
            foreach (var sm in source.OtherMethods)
                result.OtherMethods.Add(Importer.Import(sm).ResolveMethodDef());
            if (source.HasConstant)
                result.Constant = Clone(source.Constant);
            CopyCustomAttributes(source, result);
            CopyCustomDebugInfo(source, result);
        }

        void Copy(EventDef source, EventDef result)
        {
            if (source.AddMethod != null)
                result.AddMethod = Importer.Import(source.AddMethod).ResolveMethodDef();
            if (source.RemoveMethod != null)
                result.RemoveMethod = Importer.Import(source.RemoveMethod).ResolveMethodDef();
            if (source.InvokeMethod != null)
                result.InvokeMethod = Importer.Import(source.InvokeMethod).ResolveMethodDef();
            CopyCustomAttributes(source, result);
            foreach (var sm in source.OtherMethods)
                result.OtherMethods.Add(Importer.Import(sm).ResolveMethodDef());
            CopyCustomDebugInfo(source, result);
        }

        ParamDef Clone(MethodDef sourceMethod, MethodDef targetMethod, ParamDef source)
        {
            ParamDef result = new ParamDefUser(source.Name, source.Sequence, source.Attributes);
            result = Context.Fire(new NetfuserEvent.CreateMethodParameter(Context, source, targetMethod, _tm)
                {Target = result}).Target;
            if (source.HasConstant)
                result.Constant = Clone(source.Constant);
            CopyMarshalType(source, result);
            CopyCustomAttributes(source, result);
            CopyCustomDebugInfo(source, result);
            return result;
        }


        void Copy(MethodDef source, MethodDef result)
        {
            foreach (var sourceParamDef in source.ParamDefs)
                result.ParamDefs.Add(Clone(source, result, sourceParamDef));
            var isDestructor = false;
            foreach (var sourceOverride in source.Overrides)
            {
                var overriddenTarget = (IMethodDefOrRef) Importer.Import(sourceOverride.MethodDeclaration);
                result.Overrides.Add(new MethodOverride(result, overriddenTarget));
                if (sourceOverride.MethodDeclaration.FullName == "System.Void System.Object::Finalize()")
                    isDestructor = true;
            }

            if (!isDestructor && result.IsVirtual && result.IsFamily && !result.IsNewSlot)
                // if the method is overriding protected internal (famorassem) method from another assembly, we must adjust its visibility to famorassem as well
                _checkAdjustFamorassemMethods.Add(result);
            if (source.ExportInfo != null)
                result.ExportInfo = source.ExportInfo;
            if (source.HasBody)
                switch (source.MethodBody)
                {
                    case CilBody cb:
                        var bodyCloner = CilBodyMerger.Create(Context, _tm, source, result);
                        result.MethodBody = bodyCloner.Run();
                        break;
                    default:
                        throw new NotImplementedException();
                }

            CopyImplMap(source, result);
            CopyGenericParameters(source, result);
            CopyCustomAttributes(source, result);
            CopyDeclSecurities(source, result);
            CopyCustomDebugInfo(source, result);
            Context.Fire(new NetfuserEvent.MethodImported(Context, source, result, Importer));
        }

        void CheckAdjustFamorassemMethods(NetfuserEvent.TypesImported ev)
        {
            foreach (var m in _checkAdjustFamorassemMethods)
            {
                var baseType = m.DeclaringType.BaseType;
                while (baseType != null && baseType.Scope == _tm.Target.Scope)
                {
                    var baseTypeDef = baseType.ResolveTypeDef();
                    var overridenMethod = baseTypeDef.FindMethod(m.Name, m.MethodSig);
                    if (overridenMethod != null && overridenMethod.IsFamilyOrAssembly &&
                        overridenMethod.DeclaringType.Module == m.DeclaringType.Module)
                    {
                        m.Attributes |= MethodAttributes.FamORAssem;
                        break;
                    }

                    baseType = baseTypeDef.BaseType;
                }
            }
        }


        internal void ImportDeclarations()
        {
            var source = _tm.Source;
            var target = _tm.Target;
            if (!_merge)
                CopyGenericParameters(source, target);
            else if (!Equals(source.GenericParameters, target.GenericParameters))
                throw Context.Error(
                    $"can't merge types with different generic params: {source.FullName} and {target.FullName}");

            var injected = Context.Fire(new NetfuserEvent.InjectMembers(Context, _tm)).Injected;

            // methods should go first, because if other members' names clash, it's easy to change their name
            // but if we import field "a" and then we get method "a" with hundreds of virtual and interface relatives, we have a problem 
            foreach (var sourceMethod in source.Methods.Concat(injected.OfType<MethodDef>()))
            {
                MethodDef result = new MethodDefUser(sourceMethod.Name, Importer.Import(sourceMethod.MethodSig),
                    sourceMethod.ImplAttributes,
                    sourceMethod.Attributes);
                result = Context.Fire(new NetfuserEvent.CreateMethod(Context, sourceMethod, _tm) {Target = result}).Target;
                target.Methods.Add(result);
                _methods.Add(Tuple.Create(sourceMethod, result));
            }

            foreach (var sourceField in source.Fields.Concat(injected.OfType<FieldDef>()))
            {
                FieldDef result = new FieldDefUser(sourceField.Name, Importer.Import(sourceField.FieldSig),
                        sourceField.Attributes)
                    {FieldOffset = sourceField.FieldOffset};
                result = Context.Fire(new NetfuserEvent.CreateField(Context, sourceField, _tm) {Target = result}).Target;
                target.Fields.Add(result);
                _fields.Add(Tuple.Create(sourceField, result));
            }

            foreach (var sourceProperty in source.Properties.Concat(injected.OfType<PropertyDef>()))
            {
                PropertyDef result =
                    new PropertyDefUser(sourceProperty.Name, Importer.Import(sourceProperty.PropertySig),
                        sourceProperty.Attributes);
                result = Context.Fire(new NetfuserEvent.CreateProperty(Context, sourceProperty, _tm) {Target = result})
                    .Target;
                target.Properties.Add(result);
                _properties.Add(Tuple.Create(sourceProperty, result));
            }

            foreach (var sourceEvent in source.Events.Concat(injected.OfType<EventDef>()))
            {
                EventDef result = new EventDefUser(sourceEvent.Name, Importer.Import(sourceEvent.EventType),
                    sourceEvent.Attributes);
                result = Context.Fire(new NetfuserEvent.CreateEvent(Context, sourceEvent, _tm) {Target = result}).Target;
                target.Events.Add(result);
                _events.Add(Tuple.Create(sourceEvent, result));
            }
        }


        internal void Run()
        {
            var source = _tm.Source;
            var target = _tm.Target;
            if (!_merge)
            {
                if (source.BaseType != null)
                    target.BaseType = Importer.Import(source.BaseType);

                if (source.HasClassLayout)
                    target.ClassLayout =
                        new ClassLayoutUser(source.ClassLayout.PackingSize, source.ClassLayout.ClassSize);

                foreach (var si in source.Interfaces)
                    target.Interfaces.Add(Clone(si));

                CopyCustomAttributes(source, target);
                CopyCustomDebugInfo(source, target);
                CopyDeclSecurities(source, target);
            }


            foreach (var m in _fields)
                Copy(m.Item1, m.Item2);

            foreach (var m in _methods)
                Copy(m.Item1, m.Item2);

            foreach (var m in _properties)
                Copy(m.Item1, m.Item2);

            foreach (var m in _events)
                Copy(m.Item1, m.Item2);


            if (_checkAdjustFamorassemMethods.Count > 0)
                Context.OfType<NetfuserEvent.TypesImported>().Take(1).Subscribe(CheckAdjustFamorassemMethods);
        }

        internal static TypeMerger Create(ContextImpl context, TypeMapping tm, bool merge) =>
            new TypeMerger(context, context.GetImporter(tm.Target), tm, merge);
    }
}