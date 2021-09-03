﻿using Mono.Cecil;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Mutator
{
    public static class Extensions
    {
        public static TypeReference ImportTypeFromCoreLib(this ModuleDefinition module, string ns, string name)
        {
            return module.ImportReference(new TypeReference(ns, name, module, module.TypeSystem.CoreLibrary));
        }

        public static TypeReference ImportTypeFromSysCore(this ModuleDefinition module, string ns, string name)
        {
            AssemblyNameReference? asmRef = module.AssemblyReferences.FirstOrDefault(a => a.Name == "System.Core");

            if (asmRef == null) {
                asmRef = new AssemblyNameReference("System.Core", new(3, 5));
                module.AssemblyReferences.Add(asmRef);
            }

            return module.ImportReference(new TypeReference(ns, name, module, asmRef));
        }

        public static MethodReference ImportCtor(this TypeReference declaring, params TypeReference[] parameters)
        {
            return declaring.ImportMethod(false, ".ctor", declaring.Module.TypeSystem.Void, parameters);
        }

        public static MethodReference ImportMethod(this TypeReference declaring, bool isStatic, string name, TypeReference returnType, params TypeReference[] parameters)
        {
            MethodReference method = new(name, returnType, declaring) { HasThis = !isStatic };

            foreach (var param in parameters) {
                method.Parameters.Add(new(param));
            }

            return declaring.Module.ImportReference(method);
        }

        public static bool SeekTree(this TypeReference type, string fullName, [MaybeNullWhen(false)] out TypeReference accepted)
        {
            return SeekTree(type, t => t.FullName == fullName, out accepted);
        }

        public static bool SeekTree(this TypeReference type, Predicate<TypeReference> accept, [MaybeNullWhen(false)] out TypeReference accepted)
        {
            while (type != null) {
                if (accept(type)) {
                    accepted = type;
                    return true;
                }

                type = type.Resolve().BaseType;
            }

            accepted = null;
            return false;
        }

        public static string ProofDirectory(this string self)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(self)!);
            return self;
        }
    }
}
