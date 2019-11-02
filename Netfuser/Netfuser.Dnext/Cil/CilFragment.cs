using System.Collections;
using System.Collections.Generic;
using System.Text;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Cil
{
    public class CilFragment : IEnumerable<Instruction>
    {
        private volatile List<Instruction> _instructions;
        public List<Instruction> Instructions => _instructions;

        public bool IsEmpty => _instructions.Count == 0;
        public int Count => _instructions.Count;
        public Instruction First => _instructions.Count == 0 ? null : _instructions[0];
        public Instruction Last => _instructions.Count == 0 ? null : _instructions[_instructions.Count - 1];

        public CilFragment(IEnumerable<Instruction> instructions)
        {
            _instructions = new List<Instruction>(instructions);
        }

        public CilFragment()
        {
            _instructions = new List<Instruction>();
        }

        public void Reset(IEnumerable<Instruction> instructions)
        {
            _instructions = instructions as List<Instruction> ?? new List<Instruction>(instructions);
        }

        public void Add(Instruction instr)
        {
            // don't clone instructions here, as we may use them to re-assemble method body, so references in exception
            // handlers remain valid
            _instructions.Add(instr);
        }

        public void Add(IEnumerable<Instruction> instructions)
        {
            // don't clone instructions here, as we may use them to re-assemble method body, so references in exception
            // handlers remain valid
            _instructions.AddRange(instructions);
        }

        public void Write(CilBody body)
        {
            foreach (var i in _instructions)
                // don't clone instructions here, as we may use them to re-assemble method body, so references in exception
                // handlers remain valid
                body.Instructions.Add(i);
        }

        public IEnumerator<Instruction> GetEnumerator() => _instructions.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            var ret = new StringBuilder();
            foreach (var instr in _instructions)
                ret.AppendLine(instr.ToString());
            return ret.ToString();
        }
    }
}