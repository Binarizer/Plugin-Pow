using BepInEx;

namespace PathOfWuxia
{
    interface IHook
    {
        void OnRegister(BaseUnityPlugin plugin);

        void OnUpdate();
    }
}
