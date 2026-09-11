using System.Windows.Input;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Routed commands for the cover card's context menu and favourite
    /// toggle. Declared as commands (not code-behind Click= handlers)
    /// because the card template itself lives in the shared, x:Class-less
    /// <c>CoverShuffleTheme.xaml</c> resource dictionary (see Stage 1's own
    /// centralization rationale there); WPF requires an owning x:Class to
    /// wire a Click event handler directly in XAML, but routed commands are
    /// resolved via CommandBindings anywhere up the visual tree, so the
    /// dictionary itself needs no code-behind. Each consuming window (only
    /// <see cref="ManageCoversWindow"/> today) supplies the actual behaviour
    /// via its own <c>CommandBindings</c>.
    /// </summary>
    public static class CoverCardCommands
    {
        public static readonly RoutedUICommand Preview = new RoutedUICommand("Preview", nameof(Preview), typeof(CoverCardCommands));

        public static readonly RoutedUICommand SetAsCurrent = new RoutedUICommand("Set as Current", nameof(SetAsCurrent), typeof(CoverCardCommands));

        public static readonly RoutedUICommand Enable = new RoutedUICommand("Enable", nameof(Enable), typeof(CoverCardCommands));

        public static readonly RoutedUICommand Disable = new RoutedUICommand("Disable", nameof(Disable), typeof(CoverCardCommands));

        public static readonly RoutedUICommand Replace = new RoutedUICommand("Replace", nameof(Replace), typeof(CoverCardCommands));

        public static readonly RoutedUICommand Remove = new RoutedUICommand("Remove", nameof(Remove), typeof(CoverCardCommands));

        public static readonly RoutedUICommand ToggleFavorite = new RoutedUICommand("Toggle Favorite", nameof(ToggleFavorite), typeof(CoverCardCommands));
    }
}
