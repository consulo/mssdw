using System.Diagnostics.SymbolStore;
using System.Collections.Generic;
using System;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.Extensions;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata;
using System.Reflection;

namespace Consulo.Internal.Mssdw {
    public static class SequencePointExt {
        public static IEnumerable<SequencePoint> GetSequencePoints (this ISymbolMethod met) {
            int sc = met.SequencePointCount;
            int[] offsets = new int[sc];
            int[] lines = new int[sc];
            int[] endLines = new int[sc];
            int[] columns = new int[sc];
            int[] endColumns = new int[sc];
            ISymbolDocument[] docs = new ISymbolDocument[sc];
            met.GetSequencePoints(offsets, docs, lines, columns, endLines, endColumns);

            for (int n = 0; n < sc; n++) {
                SequencePoint sp = new SequencePoint();
                sp.Document = docs[n];
                sp.StartLine = lines[n];
                sp.EndLine = endLines[n];
                sp.StartColumn = columns[n];
                sp.EndColumn = endColumns[n];
                sp.Offset = offsets[n];
                yield return sp;
            }
        }

        public static Type GetTypeInfo (this CorType type, DebugSession session) {
            Type t;
            if (MetadataHelperFunctionsExtensions.CoreTypes.TryGetValue(type.Type, out t))
                return t;

            if (type.Type == CorElementType.ELEMENT_TYPE_ARRAY || type.Type == CorElementType.ELEMENT_TYPE_SZARRAY) {
                List<int> sizes = new List<int>();
                List<int> loBounds = new List<int>();
                for (int n = 0; n < type.Rank; n++) {
                    sizes.Add(1);
                    loBounds.Add(0);
                }
                return MetadataExtensions.MakeArray(type.FirstTypeParameter.GetTypeInfo(session), sizes, loBounds);
            }

            if (type.Type == CorElementType.ELEMENT_TYPE_BYREF)
                return MetadataExtensions.MakeByRef(type.FirstTypeParameter.GetTypeInfo(session));

            if (type.Type == CorElementType.ELEMENT_TYPE_PTR)
                return MetadataExtensions.MakePointer(type.FirstTypeParameter.GetTypeInfo(session));

            CorMetadataImport mi = session.GetMetadataForModule(type.Class.Module.Name);
            if (mi != null) {
                t = mi.GetType(type.Class.Token);
                CorType[] targs = type.TypeParameters;
                if (targs.Length > 0) {
                    List<Type> types = new List<Type>();
                    foreach (CorType ct in targs)
                        types.Add(ct.GetTypeInfo(session));
                    return MetadataExtensions.MakeGeneric(t, types);
                }
                else
                    return t;
            }
            else
                return null;
        }

        public static ISymbolMethod GetSymbolMethod (this CorFunction func, DebugSession session) {
            ISymbolReader reader = session.GetReaderForModule(func.Module.Name);
            if (reader == null)
                return null;
            return reader.GetMethod(new SymbolToken(func.Token));
        }

        public static MethodInfo GetMethodInfo (this CorFunction func, DebugSession session) {
            CorMetadataImport mi = session.GetMetadataForModule(func.Module.Name);
            if (mi != null)
                return mi.GetMethodInfo(func.Token);
            else
                return null;
        }

       /* public static void SetValue (this CorValRef thisVal, EvaluationContext ctx, CorValRef val) {
            CorEvaluationContext cctx = (CorEvaluationContext) ctx;
            CorObjectAdaptor actx = (CorObjectAdaptor) ctx.Adapter;
            if (actx.IsEnum(ctx, thisVal.Val.ExactType) && !actx.IsEnum(ctx, val.Val.ExactType)) {
                ValueReference vr = actx.GetMember(ctx, null, thisVal, "value__");
                vr.Value = val;
                // Required to make sure that var returns an up-to-date value object
                thisVal.IsValid = false;
                return;
            }

            CorReferenceValue s = thisVal.Val.CastToReferenceValue();
            if (s != null) {
                CorReferenceValue v = val.Val.CastToReferenceValue();
                if (v != null) {
                    s.Value = v.Value;
                    return;
                }
            }
            CorGenericValue gv = CorObjectAdaptor.GetRealObject(cctx, thisVal.Val) as CorGenericValue;
            if (gv != null)
                gv.SetValue(ctx.Adapter.TargetObjectToObject(ctx, val));
        }*/
    }
}