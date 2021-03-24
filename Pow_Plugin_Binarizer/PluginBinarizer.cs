using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx;

namespace PathOfWuxia
{
    [BepInPlugin("binarizer.plugin.pow.function_sets", "功能合集 by Binarizer", "1.01")]
    public class PluginBinarizer : BaseUnityPlugin
    {
        void RegisterHook(IHook hook)
        {
            hook.OnRegister(this);
            Harmony.CreateAndPatchAll( hook.GetType() );
            hooks.Add(hook);
        }

        private List<IHook> hooks = new List<IHook>();

        void Awake()
        {
            Console.WriteLine("美好的初始化开始");

            RegisterHook(new HookModSupport());
            RegisterHook(new HookGenerals());
            RegisterHook(new HookNewGame());
            RegisterHook(new HookFeaturesAndFixes());
            RegisterHook(new HookMoreAccessories());
            RegisterHook(new HookModExtensions());
            RegisterHook(new HookUniqueItem());
            RegisterHook(new HookInitiactiveBattle());
        }

        void Start()
        {
            Console.WriteLine("美好的第一帧开始");
            Console.WriteLine("Game.Data = " + Heluo.Game.Data.GetType());
        }

        void Update()
        {
            foreach(IHook hook in hooks)
            {
                hook.OnUpdate();
            }
        }

        private static void DisplayGenericMethodInfo(MethodBase mi)
        {
            Console.WriteLine("\r\n{0}", mi);

            Console.WriteLine("\tIs this a generic method definition? {0}",
                mi.IsGenericMethodDefinition);

            Console.WriteLine("\tIs it a generic method? {0}",
                mi.IsGenericMethod);

            Console.WriteLine("\tDoes it have unassigned generic parameters? {0}",
                mi.ContainsGenericParameters);

            // If this is a generic method, display its type arguments.
            //
            if (mi.IsGenericMethod)
            {
                Type[] typeArguments = mi.GetGenericArguments();

                Console.WriteLine("\tList type arguments ({0}):",
                    typeArguments.Length);

                foreach (Type tParam in typeArguments)
                {
                    // IsGenericParameter is true only for generic type
                    // parameters.
                    //
                    if (tParam.IsGenericParameter)
                    {
                        Console.WriteLine("\t\t{0}  parameter position {1}" +
                            "\n\t\t   declaring method: {2}",
                            tParam,
                            tParam.GenericParameterPosition,
                            tParam.DeclaringMethod);
                    }
                    else
                    {
                        Console.WriteLine("\t\t{0}", tParam);
                    }
                }
            }
        }
    }
}
