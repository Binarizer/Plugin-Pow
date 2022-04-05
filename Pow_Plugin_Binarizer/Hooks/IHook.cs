using BepInEx;
using System;
using System.Collections.Generic;

namespace PathOfWuxia
{
    interface IHook
    {
        void OnRegister(PluginBinarizer plugin);

        //void OnUnregister(PluginBinarizer plugin);
    }
}
