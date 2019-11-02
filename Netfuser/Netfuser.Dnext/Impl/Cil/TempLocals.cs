using System;
using System.Collections.Generic;
using Base.Lang;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;

namespace Netfuser.Dnext.Impl.Cil
{
    class TempLocals : ITempLocals
    {
        private readonly Dictionary<TypeSig, Queue<Local>> _freeLocals;
        private readonly List<Local> _toEmit;
        private bool _flushed;

        public TempLocals()
        {
            _freeLocals = new Dictionary<TypeSig, Queue<Local>>(TypeEqualityComparer.Instance);
            _toEmit = new List<Local>();
        }

        private void CheckFlushed()
        {
            if (_flushed) throw new Exception("temporary locals have already been flushed to the method body");
        }

        void Enqueue(TypeSig key, Local value)
        {
            if (!_freeLocals.TryGetValue(key, out var queue))
                _freeLocals.Add(key, queue = new Queue<Local>());
            queue.Enqueue(value);
        }

        bool TryDequeue(TypeSig key, out Local value)
        {
            if (_freeLocals.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                value = queue.Dequeue();
                if (queue.Count == 0)
                    _freeLocals.Remove(key);
                return true;
            }

            value = default;
            return false;
        }

        public Local Request(TypeSig type)
        {
            if (!TryDequeue(type, out var value))
            {
                CheckFlushed();
                _toEmit.Add(value = new Local(type, null, -1));
            }

            return value;
        }

        public IDisposable Use(TypeSig type, out Local local) => new User(this, local = Request(type));

        public void Release(Local local)
        {
            if (local != null)
                Enqueue(local.Type, local);
        }

        public void Add(IEnumerable<Local> locals)
        {
            foreach (var l in locals)
                Release(l);
        }

        public void Flush(CilBody body)
        {
            CheckFlushed();
            _flushed = true;
            if (_toEmit.Count > 0)
                body.InitLocals = true;
            foreach (var local in _toEmit)
                body.Variables.Add(local);
            _toEmit.Clear();
        }

        class User : Disposable
        {
            private readonly TempLocals _locals;
            private readonly Local _local;

            public User(TempLocals locals, Local local)
            {
                _locals = locals;
                _local = local;
            }

            protected override void OnDispose()
            {
                _locals.Release(_local);
            }
        }
    }
}