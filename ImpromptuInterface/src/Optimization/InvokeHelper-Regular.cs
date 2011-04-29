﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ImpromptuInterface.Build;
using ImpromptuInterface.Dynamic;
using Microsoft.CSharp.RuntimeBinder;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace ImpromptuInterface.Optimization
{

    internal class DummmyNull
    {

    }

    internal static partial class InvokeHelper
    {
        internal static object InvokeMethodDelegate(this object target, Delegate tFunc, object[] args)
        {
            object result;
            try
            {
                result = tFunc.FastDynamicInvoke(
                    tFunc.IsSpecialThisDelegate()
                        ? new[] { target }.Concat(args).ToArray()
                        : args
                    );
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;
                throw;
            }
            return result;
        }

        private static readonly IDictionary<BinderHash, CallSite> _binderCache = new Dictionary<BinderHash, CallSite>();


        private static readonly object _binderCacheLock = new object();

        /// <summary>
        /// LazyBinderType
        /// </summary>
        internal delegate CallSiteBinder LazyBinder();


        internal static IEnumerable<CSharpArgumentInfo> GetBindingArgumentList(object[] args, string[] argNames, bool staticContext)
        {

            var tTargetFlag = CSharpArgumentInfoFlags.None;
            if (staticContext)
            {
                tTargetFlag |= CSharpArgumentInfoFlags.IsStaticType | CSharpArgumentInfoFlags.UseCompileTimeType;
            }



            var tList = new BareBonesList<CSharpArgumentInfo>(args.Length + 1)
                        {
                            CSharpArgumentInfo.Create(tTargetFlag, null)
                        };

            //Optimization: linq statement creates a slight overhead in this case
            // ReSharper disable LoopCanBeConvertedToQuery
            // ReSharper disable ForCanBeConvertedToForeach
            for (int i = 0; i < args.Length; i++)
            {
                var tFlag = CSharpArgumentInfoFlags.None;
                string tName = null;
                if (argNames != null && argNames.Length > i)
                    tName = argNames[i];

                if (!String.IsNullOrEmpty(tName))
                {
                    tFlag |= CSharpArgumentInfoFlags.NamedArgument;

                }
                tList.Add(CSharpArgumentInfo.Create(
                    tFlag, tName));
            }
            // ReSharper restore ForCanBeConvertedToForeach
            // ReSharper restore LoopCanBeConvertedToQuery

            return tList;
        }


        internal static CallSite CreateCallSite(
            Type delegateType,
            Type specificBinderType,
            LazyBinder binder,
            String_OR_InvokeMemberName name,
            Type context,
            string[] argNames = null,
            bool staticContext = false,
            bool isEvent = false
            )
        {
            
            var tHash = BinderHash.Create(delegateType, name, context, argNames, specificBinderType, staticContext, isEvent);
            lock (_binderCacheLock)
            {
                CallSite tOut;
                if (!_binderCache.TryGetValue(tHash, out tOut))
                {
                    tOut = CallSite.Create(delegateType, binder());
                    _binderCache[tHash] = tOut;
                }
                return tOut;
            }
        }


        internal static CallSite<T> CreateCallSite<T>(
        Type specificBinderType,
        LazyBinder binder,
        String_OR_InvokeMemberName name,
        Type context,
        string[] argNames = null,
        bool staticContext = false,
        bool isEvent = false
        )
        where T : class
        {
            var tHash = BinderHash<T>.Create(name, context, argNames, specificBinderType, staticContext, isEvent);
            lock (_binderCacheLock)
            {
                CallSite tOut;
                if (!_binderCache.TryGetValue(tHash, out tOut))
                {
                    tOut = CallSite<T>.Create(binder());
                    _binderCache[tHash] = tOut;
                }
                return (CallSite<T>)tOut;
            }
        }


        internal delegate object DynamicInvokeMemberConstructorValueType(
            CallSite funcSite,
            Type funcTarget,
            ref CallSite callsite,
            Type binderType,
            LazyBinder binder,
            String_OR_InvokeMemberName name,
            bool staticContext,
            Type context,
            string[] argNames,
            Type target,
            object[] args);

        internal static readonly IDictionary<Type, CallSite<DynamicInvokeMemberConstructorValueType>> _dynamicInvokeMemberSite = new Dictionary<Type, CallSite<DynamicInvokeMemberConstructorValueType>>();

        internal static dynamic DynamicInvokeStaticMember(Type tReturn, ref CallSite callsite, Type binderType, LazyBinder binder,
                                       string name,
                                     bool staticContext,
                                     Type context,
                                     string[] argNames,
                                     Type target, params object[] args)
        {
            CallSite<DynamicInvokeMemberConstructorValueType> tSite;
            if (!_dynamicInvokeMemberSite.TryGetValue(tReturn, out tSite))
            {
                tSite = CallSite<DynamicInvokeMemberConstructorValueType>.Create(
                        Binder.InvokeMember(
                            CSharpBinderFlags.None,
                            "InvokeMemberTargetType",
                            new[] { typeof(Type), tReturn },
                            typeof(InvokeHelper),
                            new[]
                                {
                                    CSharpArgumentInfo.Create(
                                        CSharpArgumentInfoFlags.IsStaticType |
                                        CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                     CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType | CSharpArgumentInfoFlags.IsRef, null),
                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                }
                            )
                    );
                _dynamicInvokeMemberSite[tReturn] = tSite;
            }

            return tSite.Target(tSite, typeof(InvokeHelper), ref callsite, binderType, binder, name, staticContext, context, argNames, target, args);
        }


        internal static TReturn InvokeMember<TReturn>(ref CallSite callsite, Type binderType, LazyBinder binder,
                                       String_OR_InvokeMemberName name,
                                     bool staticContext,
                                     Type context,
                                     string[] argNames,
                                     object target, params object[] args)
        {
            return InvokeMemberTargetType<object, TReturn>(ref callsite, binderType, binder, name, staticContext, context, argNames, target, args);
        }

        internal static object InvokeGetCallSite(object target, string name, Type context, bool staticContext, ref CallSite callsite)
        {
            if (callsite == null)
            {
                var tTargetFlag = CSharpArgumentInfoFlags.None;
                LazyBinder tBinder;
                Type tBinderType;
                if (staticContext) //CSharp Binder won't call Static properties, grrr.
                {
                    var tStaticFlag = CSharpBinderFlags.None;
                    if (Util.IsMono) //Mono only works if InvokeSpecialName is set and .net only works if it isn't
                        tStaticFlag |= CSharpBinderFlags.InvokeSpecialName;

                    tBinder = ()=>Binder.InvokeMember(tStaticFlag, "get_" + name,
                                                         null,
                                                         context,
                                                         new List<CSharpArgumentInfo>
                                                             {
                                                                 CSharpArgumentInfo.Create(
                                                                     CSharpArgumentInfoFlags.IsStaticType |
                                                                     CSharpArgumentInfoFlags.UseCompileTimeType,
                                                                     null)
                                                             });

                    tBinderType = typeof(InvokeMemberBinder);
                }
                else
                {

                    tBinder =()=> Binder.GetMember(CSharpBinderFlags.None, name,
                                                      context,
                                                      new List<CSharpArgumentInfo>
                                                          {
                                                              CSharpArgumentInfo.Create(
                                                                  tTargetFlag, null)
                                                          });
                    tBinderType = typeof(GetMemberBinder);

                }


                callsite = CreateCallSite<Func<CallSite, object, object>>(tBinderType, tBinder, name, context);
            }
            var tCallSite = (CallSite<Func<CallSite, object, object>>) callsite;
            return tCallSite.Target(tCallSite, target);
            
        }

        internal static void InvokeSetCallSite(object target, string name, object value, Type context, bool staticContext, ref CallSite callSite)
        {
            if (callSite == null)
            {
                LazyBinder tBinder;
                Type tBinderType;
                if (staticContext) //CSharp Binder won't call Static properties, grrr.
                {

                    tBinder = () =>{
                                    var tStaticFlag = CSharpBinderFlags.ResultDiscarded;
                                    if (Util.IsMono) //Mono only works if InvokeSpecialName is set and .net only works if it isn't
                                        tStaticFlag |= CSharpBinderFlags.InvokeSpecialName;

                                      return Binder.InvokeMember(tStaticFlag, "set_" + name,
                                                          null,
                                                          context,
                                                          new List<CSharpArgumentInfo>
                                                              {
                                                                  CSharpArgumentInfo.Create(
                                                                      CSharpArgumentInfoFlags.IsStaticType |
                                                                      CSharpArgumentInfoFlags.UseCompileTimeType, null),
                                                                  CSharpArgumentInfo.Create(

                                                                      CSharpArgumentInfoFlags.None

                                                                      , null)
                                                              });
                                  };

                    tBinderType = typeof(InvokeMemberBinder);

                }
                else
                {

                    tBinder = ()=> Binder.SetMember(CSharpBinderFlags.ResultDiscarded, name,
                                               context,
                                               new List<CSharpArgumentInfo>
                                                   {
                                                       CSharpArgumentInfo.Create(
                                                           CSharpArgumentInfoFlags.None, null),
                                                       CSharpArgumentInfo.Create(

                                                           CSharpArgumentInfoFlags.None

                                                           , null)

                                                   });


                    tBinderType = typeof(SetMemberBinder);
                }


                callSite = CreateCallSite<Action<CallSite, object, object>>(tBinderType, tBinder, name, context);
            }
            var tCallSite = (CallSite<Action<CallSite, object, object>>) callSite;
            tCallSite.Target(callSite, target, value);
        }

        internal static object InvokeMemberCallSite(object target,  String_OR_InvokeMemberName name, object[] args, string[] tArgNames, Type tContext, bool tStaticContext, ref CallSite callSite)
        {
            LazyBinder tBinder = null;
            Type tBinderType = null;
            if (callSite == null)
            {
              
                tBinder = () =>
                {
                                var tList = GetBindingArgumentList(args, tArgNames, tStaticContext);
                                var tFlag = CSharpBinderFlags.None;
                                if (name.IsSpecialName)
                                {
                                    tFlag |= CSharpBinderFlags.InvokeSpecialName;
                                }
                                 return Binder.InvokeMember(tFlag, name.Name, name.GenericArgs,
                                                             tContext, tList);
                              };
                tBinderType = typeof (InvokeMemberBinder);
            }


            return InvokeMember<object>(ref callSite, tBinderType, tBinder, name, tStaticContext, tContext, tArgNames, target, args);
        }

        internal static object InvokeGetIndexCallSite(object target, object[] indexes, string[] argNames, Type context, bool tStaticContext,ref CallSite callSite)
        {
            LazyBinder tBinder=null;
            Type tBinderType = null;
            if (callSite == null)
            {

                tBinder = () =>
                              {
                                  var tList = GetBindingArgumentList(indexes, argNames,
                                                                               tStaticContext);
                                  return Binder.GetIndex(CSharpBinderFlags.None, context, tList);
                              };
                tBinderType = typeof (GetIndexBinder);

            }

            return InvokeMember<object>(ref callSite,tBinderType, tBinder, Invocation.IndexBinderName, tStaticContext, context, argNames, target, indexes);
        }

        internal static void InvokeSetIndexCallSite(object target, object[] indexesThenValue, string[] tArgNames, Type tContext, bool tStaticContext, CallSite tCallSite)
        {
            LazyBinder tBinder =null;
            Type tBinderType = null;
            if (tCallSite == null)
            {

                tBinder = () =>
                              {
                                  var tList = GetBindingArgumentList(indexesThenValue, tArgNames,
                                                                               tStaticContext);
                                  return Binder.SetIndex(CSharpBinderFlags.None, tContext, tList);
                              };

                tBinderType = typeof (SetIndexBinder);
            }

            InvokeMemberAction(ref tCallSite, tBinderType, tBinder, Invocation.IndexBinderName, tStaticContext, tContext, tArgNames, target, indexesThenValue);
        }

        internal static void InvokeMemberActionCallSite(object target,String_OR_InvokeMemberName name, object[] args, string[] tArgNames, Type tContext, bool tStaticContext,ref CallSite callSite)
        {
            LazyBinder tBinder =null;
            Type tBinderType = null;
            if (callSite == null)
            {

                tBinder = () =>
                              {
                                  IEnumerable<CSharpArgumentInfo> tList;
                                  tList = GetBindingArgumentList(args, tArgNames, tStaticContext);

                                  var tFlag = CSharpBinderFlags.ResultDiscarded;
                                  if (name.IsSpecialName)
                                  {
                                      tFlag |= CSharpBinderFlags.InvokeSpecialName;
                                  }

                                  return Binder.InvokeMember(tFlag, name.Name, name.GenericArgs,
                                                             tContext, tList);
                              };
                tBinderType = typeof (InvokeMemberBinder);
            }


            InvokeMemberAction(ref callSite,tBinderType, tBinder, name, tStaticContext, tContext, tArgNames, target, args);
        }
        internal class IsEventBinderDummy{
            
        }
        internal static bool InvokeIsEventCallSite(object target, string name, Type tContext, ref CallSite callSite)
        {
            if (callSite == null)
            {
                LazyBinder tBinder = ()=> Binder.IsEvent(CSharpBinderFlags.None, name, tContext);
                var tBinderType = typeof (IsEventBinderDummy);
                callSite = CreateCallSite<Func<CallSite, object, bool>>(tBinderType, tBinder, name, tContext, isEvent: true);
            }
            var tCallSite = (CallSite<Func<CallSite, object, bool>>)callSite;

            return tCallSite.Target(tCallSite, target);
        }

        internal static void InvokeAddAssignCallSite(object target, string name, object[] args, string[] argNames, Type context, bool staticContext, ref CallSite callSiteIsEvent, ref CallSite callSiteAdd, ref CallSite callSiteGet, ref CallSite callSiteSet)
        {

            if (InvokeIsEventCallSite(target, name, context, ref callSiteIsEvent))
            {
                InvokeMemberActionCallSite(target, InvokeMemberName.CreateSpecialName("add_" + name), args, argNames, context, staticContext, ref callSiteAdd);
            }
            else
            {
                dynamic tGet = InvokeGetCallSite(target,name, context, staticContext, ref callSiteGet);
                tGet += (dynamic)(args[0]);
                InvokeSetCallSite(target, name,  (object)tGet, context, staticContext, ref callSiteSet);
            }
        }

        internal static void InvokeSubtractAssignCallSite(object target, string name, object[] args, string[] argNames, Type context, bool staticContext, ref CallSite callSiteIsEvent, ref CallSite callSiteRemove, ref CallSite callSiteGet, ref CallSite callSiteSet)
        {
            if (InvokeIsEventCallSite(target, name, context, ref callSiteIsEvent))
            {
                InvokeMemberActionCallSite(target, InvokeMemberName.CreateSpecialName("remove_" + name), args, argNames, context, staticContext, ref callSiteRemove);
            }
            else
            {
                dynamic tGet = InvokeGetCallSite(target, name, context, staticContext, ref callSiteGet);
                tGet -= (dynamic)(args[0]);
                InvokeHelper.InvokeSetCallSite(target, name, tGet, context, staticContext, ref callSiteSet);
            }
        }

        internal static object InvokeConvertCallSite(object target, bool explict, Type type, Type context, ref CallSite callSite)
        {
            if (callSite == null)
            {
                LazyBinder tBinder = () =>
                                         {
                                             var tFlags = explict ? CSharpBinderFlags.ConvertExplicit : CSharpBinderFlags.None;

                                             return Binder.Convert(tFlags, type, context);
                                         };
                Type tBinderType = typeof (ConvertBinder);

                var tFunc = BuildProxy.GenerateCallSiteFuncType(new Type[] {}, type);


                callSite = CreateCallSite(tFunc, tBinderType, tBinder,
                                          explict
                                              ? Invocation.ExplicitConvertBinderName
                                              : Invocation.ImplicitConvertBinderName, context);
            }
            dynamic tDynCallSite = callSite;
            return tDynCallSite.Target(callSite, target);

        }

        internal class InvokeConstructorDummy{};

        internal static object InvokeConstructorCallSite(Type type, bool isValueType, object[] args, string[] argNames,Type context, ref CallSite callSite)
        {
            LazyBinder tBinder = null;
            Type tBinderType  = typeof (InvokeConstructorDummy);
            if (callSite == null || isValueType)
            {
                if (isValueType && args.Length == 0)  //dynamic invocation doesn't see no argument constructors of value types
                {
                    return Activator.CreateInstance(type);
                }


                tBinder = () =>
                              {
                                  var tList = GetBindingArgumentList(args, argNames, true);
                                  return Binder.InvokeConstructor(CSharpBinderFlags.None, type, tList);
                              };
            }


            if (isValueType || Util.IsMono)
            {
                CallSite tDummy =null;
                return DynamicInvokeStaticMember(type, ref tDummy,tBinderType, tBinder, Invocation.ConstructorBinderName, true, type,
                                                              argNames, type, args);
            }

            return InvokeMemberTargetType<Type, object>(ref callSite,tBinderType, tBinder, Invocation.ConstructorBinderName, true, type, argNames,
                                                                     type, args);
        }
    }
}