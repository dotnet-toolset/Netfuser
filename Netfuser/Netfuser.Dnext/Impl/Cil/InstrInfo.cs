using System.Collections.Generic;
using Base.Collections;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;

namespace Netfuser.Dnext.Impl.Cil
{
    class InstrInfo : IInstrInfo
    {
        private List<Instruction> _referencedBy;
        private List<string> _errors;

        private int _depthBefore, _depthAfter;

        internal bool IsDepthSet { get; private set; }

        public int DepthBefore
        {
            get => _depthBefore;
            internal set
            {
                _depthBefore = value;
                IsDepthSet = true;
            }
        }

        public int DepthAfter
        {
            get => _depthAfter;
            internal set
            {
                _depthAfter = value;
                IsDepthSet = true;
            }
        }

        public bool NaturalTarget { get; internal set; } = true;
        public IReadOnlyList<Instruction> ReferencedBy => _referencedBy.OrEmpty();
        public IReadOnlyList<string> Errors => _errors.OrEmpty();
        public int RefCount => (_referencedBy?.Count ?? 0) + (NaturalTarget ? 1 : 0);

        public void Error(string text)
        {
            if (_errors == null)
                _errors = new List<string>();
            _errors.Add(text);
        }

        public void Ref(Instruction i)
        {
            if (_referencedBy == null)
                _referencedBy = new List<Instruction>();
            _referencedBy.Add(i);
        }
    }
}