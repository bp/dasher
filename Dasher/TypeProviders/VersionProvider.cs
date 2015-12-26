using System;
using System.Reflection.Emit;

namespace Dasher.TypeProviders
{
    internal sealed class VersionProvider : ITypeProvider
    {
        public bool CanProvide(Type type) => type == typeof(Version);

        public void Serialise(ILGenerator ilg, LocalBuilder value, LocalBuilder packer)
        {
            // write the string form
            ilg.Emit(OpCodes.Ldloc, packer);
            ilg.Emit(OpCodes.Ldloc, value);
            ilg.Emit(OpCodes.Call, typeof(Version).GetMethod(nameof(Version.ToString), new Type[0]));
            ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.Pack), new[] { typeof(string) }));
        }

        public void Deserialise(ILGenerator ilg, LocalBuilder value, LocalBuilder unpacker, string name, Type targetType)
        {
            // read value as string
            var s = ilg.DeclareLocal(typeof(string));

            ilg.Emit(OpCodes.Ldloc, unpacker);
            ilg.Emit(OpCodes.Ldloca, s);
            ilg.Emit(OpCodes.Call, typeof(Unpacker).GetMethod(nameof(Unpacker.TryReadString), new[] {typeof(string).MakeByRefType()}));

            // If the unpacker method failed (returned false), throw
            var lbl = ilg.DefineLabel();
            ilg.Emit(OpCodes.Brtrue, lbl);
            {
                ilg.Emit(OpCodes.Ldstr, $"Expecting string value for Version property {name}");
                ilg.LoadType(targetType);
                ilg.Emit(OpCodes.Newobj, typeof(DeserialisationException).GetConstructor(new[] { typeof(string), typeof(Type) }));
                ilg.Emit(OpCodes.Throw);
            }
            ilg.MarkLabel(lbl);

            ilg.Emit(OpCodes.Ldloc, s);
            ilg.Emit(OpCodes.Newobj, typeof(Version).GetConstructor(new[] { typeof(string) }));
            ilg.Emit(OpCodes.Stloc, value);
        }
    }
}