using BepInEx;
using System;
using System.Collections.Generic;

namespace PathOfWuxia
{
    interface IHook
    {
        IEnumerable<Type> GetRegisterTypes();

        void OnRegister(BaseUnityPlugin plugin);

        void OnUpdate();
    }
}
