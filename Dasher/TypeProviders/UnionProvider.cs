﻿#region License
//
// Dasher
//
// Copyright 2015-2016 Drew Noakes
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
// More information about this project is available at:
//
//    https://github.com/drewnoakes/dasher
//
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Dasher.TypeProviders
{
    public sealed class UnionProvider : ITypeProvider
    {
        // Union types are serialised as an array of two values:
        //   The string name of the type, including namespace and any generic type parameters
        //   The serialised value, as per regular Dasher serialisation

        public bool CanProvide(Type type)
            => type.IsGenericType &&
               type.GetGenericTypeDefinition().Namespace == nameof(Dasher) &&
               type.GetGenericTypeDefinition().Name.StartsWith($"{nameof(Union<int, int>)}`");

        public bool TryEmitSerialiseCode(ILGenerator ilg, ICollection<string> errors, LocalBuilder value, LocalBuilder packer, LocalBuilder contextLocal, DasherContext context)
        {
            // write header
            ilg.Emit(OpCodes.Ldloc, packer);
            ilg.Emit(OpCodes.Ldc_I4_2);
            ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.PackArrayHeader)));

            // TODO might be faster if we a generated class having members for use with called 'Union<>.Match'

            var typeObj = ilg.DeclareLocal(typeof(Type));
            ilg.Emit(OpCodes.Ldloc, value);
            ilg.Emit(OpCodes.Callvirt, value.LocalType.GetProperty(nameof(Union<int, int>.Type)).GetMethod);
            ilg.Emit(OpCodes.Stloc, typeObj);

            // write type name
            ilg.Emit(OpCodes.Ldloc, packer);
            ilg.Emit(OpCodes.Ldloc, typeObj);
            ilg.Emit(OpCodes.Call, typeof(UnionProvider).GetMethod(nameof(GetTypeName), BindingFlags.Static | BindingFlags.Public));
            ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.Pack), new[] { typeof(string) }));

            var success = true;

            // loop through types within the union, looking for a match
            var doneLabel = ilg.DefineLabel();
            var labelNextType = ilg.DefineLabel();
            foreach (var type in value.LocalType.GetGenericArguments())
            {
                ilg.LoadType(type);
                ilg.Emit(OpCodes.Ldloc, typeObj);
                ilg.Emit(OpCodes.Call, typeof(object).GetMethod(nameof(object.Equals), BindingFlags.Static | BindingFlags.Public));

                // continue if this type doesn't match the union's values
                ilg.Emit(OpCodes.Brfalse, labelNextType);

                // we have a match

                // get the value
                var valueObj = ilg.DeclareLocal(type);
                ilg.Emit(OpCodes.Ldloc, value);
                ilg.Emit(OpCodes.Callvirt, value.LocalType.GetProperty(nameof(Union<int, int>.Value)).GetMethod);
                ilg.Emit(type.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, type);
                ilg.Emit(OpCodes.Stloc, valueObj);

                // write value
                if (!SerialiserEmitter.TryEmitSerialiseCode(ilg, errors, valueObj, packer, context, contextLocal))
                {
                    errors.Add($"Unable to serialise union member type {type}");
                    success = false;
                }

                ilg.Emit(OpCodes.Br, doneLabel);

                ilg.MarkLabel(labelNextType);
                labelNextType = ilg.DefineLabel();
            }

            ilg.MarkLabel(labelNextType);

            ilg.Emit(OpCodes.Ldstr, "No match on union type");
            ilg.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new[] {typeof(string)}));
            ilg.Emit(OpCodes.Throw);

            ilg.MarkLabel(doneLabel);

            return success;
        }

        public bool TryEmitDeserialiseCode(ILGenerator ilg, ICollection<string> errors, string name, Type targetType, LocalBuilder value, LocalBuilder unpacker, LocalBuilder contextLocal, DasherContext context, UnexpectedFieldBehaviour unexpectedFieldBehaviour)
        {
            // read the array length
            var count = ilg.DeclareLocal(typeof(int));
            ilg.Emit(OpCodes.Ldloc, unpacker);
            ilg.Emit(OpCodes.Ldloca, count);
            ilg.Emit(OpCodes.Call, typeof(Unpacker).GetMethod(nameof(Unpacker.TryReadArrayLength)));

            var lbl0 = ilg.DefineLabel();
            ilg.Emit(OpCodes.Brtrue, lbl0);
            {
                ilg.Emit(OpCodes.Ldstr, "Union values must be encoded as an array for property \"{0}\" of type \"{1}\"");
                ilg.Emit(OpCodes.Ldstr, name);
                ilg.LoadType(value.LocalType);
                ilg.Emit(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) }));
                ilg.LoadType(targetType);
                ilg.Emit(OpCodes.Newobj, typeof(DeserialisationException).GetConstructor(new[] { typeof(string), typeof(Type) }));
                ilg.Emit(OpCodes.Throw);
            }
            ilg.MarkLabel(lbl0);

            // ensure we have two items in the array
            var readValueLabel = ilg.DefineLabel();
            ilg.Emit(OpCodes.Ldloc, count);
            ilg.Emit(OpCodes.Ldc_I4_2);
            ilg.Emit(OpCodes.Beq, readValueLabel);
            {
                // throw due to incorrect number of items in Union array
                ilg.Emit(OpCodes.Ldstr, "Union array should have 2 elements (not {0}) for property \"{1}\" of type \"{2}\"");
                ilg.Emit(OpCodes.Ldloc, count);
                ilg.Emit(OpCodes.Box, typeof(int));
                ilg.Emit(OpCodes.Ldstr, name);
                ilg.LoadType(value.LocalType);
                ilg.Emit(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), new[] {typeof(string), typeof(object), typeof(object), typeof(object)}));
                ilg.LoadType(targetType);
                ilg.Emit(OpCodes.Newobj, typeof(DeserialisationException).GetConstructor(new[] {typeof(string), typeof(Type)}));
                ilg.Emit(OpCodes.Throw);
            }
            ilg.MarkLabel(readValueLabel);

            // read the serialised type name
            var typeName = ilg.DeclareLocal(typeof(string));
            ilg.Emit(OpCodes.Ldloc, unpacker);
            ilg.Emit(OpCodes.Ldloca, typeName);
            ilg.Emit(OpCodes.Call, typeof(Unpacker).GetMethod(nameof(Unpacker.TryReadString), new[] { typeof(string).MakeByRefType() }));

            var lbl1 = ilg.DefineLabel();
            ilg.Emit(OpCodes.Brtrue, lbl1);
            {
                ilg.Emit(OpCodes.Ldstr, "Unable to read union type name for property \"{0}\" of type \"{1}\"");
                ilg.Emit(OpCodes.Ldstr, name);
                ilg.LoadType(value.LocalType);
                ilg.Emit(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) }));
                ilg.LoadType(targetType);
                ilg.Emit(OpCodes.Newobj, typeof(DeserialisationException).GetConstructor(new[] { typeof(string), typeof(Type) }));
                ilg.Emit(OpCodes.Throw);
            }
            ilg.MarkLabel(lbl1);

            var success = true;

            // loop through types within the union, looking for a matching type name
            var doneLabel = ilg.DefineLabel();
            var labelNextType = ilg.DefineLabel();
            foreach (var type in value.LocalType.GetGenericArguments())
            {
                var expectedTypeName = GetTypeName(type);

                ilg.Emit(OpCodes.Ldloc, typeName);
                ilg.Emit(OpCodes.Ldstr, expectedTypeName);
                ilg.Emit(OpCodes.Call, typeof(string).GetMethod(nameof(string.Equals), BindingFlags.Static | BindingFlags.Public, null, new[] {typeof(string),typeof(string)}, null));

                // continue if this type doesn't match the union's values
                ilg.Emit(OpCodes.Brfalse, labelNextType);

                // we have a match
                // read the value
                var readValue = ilg.DeclareLocal(type);
                if (!DeserialiserEmitter.TryEmitDeserialiseCode(ilg, errors, name, targetType, readValue, unpacker, context, contextLocal, unexpectedFieldBehaviour))
                {
                    errors.Add($"Unable to deserialise union member type {type}");
                    success = false;
                }

                // create the union
                ilg.Emit(OpCodes.Ldloc, readValue);
                ilg.Emit(OpCodes.Call, value.LocalType.GetMethod(nameof(Union<int, int>.Create), new[] {type}));

                // store it in the result value
                ilg.Emit(OpCodes.Stloc, value);

                // exit the loop
                ilg.Emit(OpCodes.Br, doneLabel);

                ilg.MarkLabel(labelNextType);
                labelNextType = ilg.DefineLabel();
            }

            ilg.MarkLabel(labelNextType);

            // TODO include received type name in error message and some more general info
            ilg.Emit(OpCodes.Ldstr, "No match on union type");
            ilg.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new[] { typeof(string) }));
            ilg.Emit(OpCodes.Throw);

            ilg.MarkLabel(doneLabel);

            return success;
        }

        public static string GetTypeName(Type type)
        {
            if (!type.IsGenericType)
                return type.Namespace == nameof(System) ? type.Name : type.FullName;

            var arguments = type.GetGenericArguments();
            if (arguments.Length == 1 && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
                return $"[{GetTypeName(arguments[0])}]";
            if (arguments.Length == 2 && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                return $"({GetTypeName(arguments[0])}=>{GetTypeName(arguments[1])})";

            var baseName = type.FullName.StartsWith("Dasher.Union`")
                ? "Union"
                : type.FullName.Substring(0, type.FullName.IndexOf('`'));

            return $"{baseName}<{string.Join(",", arguments.Select(GetTypeName))}>";
        }
    }
}