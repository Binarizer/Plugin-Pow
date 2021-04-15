#pragma warning disable 0169, 0414, 0649
internal sealed class ConfigurationManagerAttributes
{
    /// <summary>
    /// Should the setting be shown as a percentage (only use with value range settings).
    /// </summary>
    public bool? ShowRangeAsPercent;

    /// <summary>
    /// Custom setting editor (OnGUI code that replaces the default editor provided by ConfigurationManager).
    /// See below for a deeper explanation. Using a custom drawer will cause many of the other fields to do nothing.
    /// </summary>
    public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;

    /// <summary>
    /// Show this setting in the settings screen at all? If false, don't show.
    /// </summary>
    public bool? Browsable;

    /// <summary>
    /// Category the setting is under. Null to be directly under the plugin.
    /// </summary>
    public string Category;

    /// <summary>
    /// If set, a "Default" button will be shown next to the setting to allow resetting to default.
    /// </summary>
    public object DefaultValue;

    /// <summary>
    /// Force the "Reset" button to not be displayed, even if a valid DefaultValue is available. 
    /// </summary>
    public bool? HideDefaultButton;

    /// <summary>
    /// Force the setting name to not be displayed. Should only be used with a <see cref="CustomDrawer"/> to get more space.
    /// Can be used together with <see cref="HideDefaultButton"/> to gain even more space.
    /// </summary>
    public bool? HideSettingName;

    /// <summary>
    /// Optional description shown when hovering over the setting.
    /// Not recommended, provide the description when creating the setting instead.
    /// </summary>
    public string Description;

    /// <summary>
    /// Name of the setting.
    /// </summary>
    public string DispName;

    /// <summary>
    /// Order of the setting on the settings list relative to other settings in a category.
    /// 0 by default, higher number is higher on the list.
    /// </summary>
    public int? Order;

    /// <summary>
    /// Only show the value, don't allow editing it.
    /// </summary>
    public bool? ReadOnly;

    /// <summary>
    /// If true, don't show the setting by default. User has to turn on showing advanced settings or search for it.
    /// </summary>
    public bool? IsAdvanced;

    /// <summary>
    /// Custom converter from setting type to string for the built-in editor textboxes.
    /// </summary>
    public System.Func<object, string> ObjToStr;

    /// <summary>
    /// Custom converter from string to setting type for the built-in editor textboxes.
    /// </summary>
    public System.Func<string, object> StrToObj;
}