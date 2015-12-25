using System;
using System.Reflection.Emit;

namespace Dasher.TypeProviders
{
    internal sealed class DecimalProvider : ITypeProvider
    {
        public bool CanProvide(Type type) => type == typeof(decimal);

        public void Serialise(ILGenerator ilg, LocalBuilder value, LocalBuilder packer)
        {
            // write the string form of the value
            ilg.Emit(OpCodes.Ldloc, packer);
            ilg.Emit(OpCodes.Ldloca, value);
            ilg.Emit(OpCodes.Call, typeof(decimal).GetMethod(nameof(decimal.ToString), new Type[0]));
            ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.Pack), new[] { typeof(string) }));
        }

        public void Deserialise(ILGenerator ilg, LocalBuilder value, LocalBuilder unpacker, string name, Type targetType)
        {
            // Read value as a string
            var s = ilg.DeclareLocal(typeof(string));

            ilg.Emit(OpCodes.Ldloc, unpacker);
            ilg.Emit(OpCodes.Ldloca, s);
            ilg.Emit(OpCodes.Call, typeof(Unpacker).GetMethod(nameof(Unpacker.TryReadString), new[] { typeof(string).MakeByRefType() }));

            ilg.Emit(OpCodes.Ldloc, s);
            ilg.Emit(OpCodes.Ldloca, value);
            ilg.Emit(OpCodes.Call, typeof(decimal).GetMethod(nameof(decimal.TryParse), new[] { typeof(string), typeof(decimal).MakeByRefType() }));

            ilg.Emit(OpCodes.And);

            // If the unpacker method failed (returned false), throw
            var lbl = ilg.DefineLabel();
            ilg.Emit(OpCodes.Brtrue, lbl);
            {
                // TODO throw better exception
                ilg.Emit(OpCodes.Ldstr, "TEST THIS CASE 4b");
                ilg.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new[] { typeof(string) }));
                ilg.Emit(OpCodes.Throw);
            }
            ilg.MarkLabel(lbl);
        }
    }
}