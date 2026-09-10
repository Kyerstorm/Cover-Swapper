using System;
using System.Collections.Generic;
using System.Windows;
using Playnite.SDK;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>
    /// Minimal fake of <see cref="IDialogsFactory"/> for testing menu wiring
    /// without a live Playnite installation. Only the members Cover Shuffle
    /// actually calls are implemented; everything else is unsupported.
    /// </summary>
    public class FakeDialogsFactory : IDialogsFactory
    {
        public List<string> ShownMessages { get; } = new List<string>();

        /// <summary>File path <see cref="SelectImagefile()"/> returns; null simulates the user cancelling.</summary>
        public string NextSelectedImageFile { get; set; }

        /// <summary>Result the next yes/no-style <see cref="ShowMessage(string, string, MessageBoxButton)"/> call returns.</summary>
        public MessageBoxResult NextMessageBoxResult { get; set; } = MessageBoxResult.Yes;

        public MessageBoxResult ShowMessage(string messageBoxText)
        {
            ShownMessages.Add(messageBoxText);
            return MessageBoxResult.OK;
        }

        public MessageBoxResult ShowMessage(string messageBoxText, string caption) => throw new NotImplementedException();

        public MessageBoxResult ShowMessage(string messageBoxText, string caption, MessageBoxButton button)
        {
            ShownMessages.Add(messageBoxText);
            return NextMessageBoxResult;
        }

        public MessageBoxResult ShowMessage(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) => throw new NotImplementedException();
        public MessageBoxOption ShowMessage(string messageBoxText, string caption, MessageBoxImage icon, List<MessageBoxOption> options) => throw new NotImplementedException();
        public MessageBoxResult ShowErrorMessage(string message) => throw new NotImplementedException();
        public MessageBoxResult ShowErrorMessage(string message, string caption) => throw new NotImplementedException();
        /// <summary>Folder path <see cref="SelectFolder()"/> returns; null simulates the user cancelling.</summary>
        public string NextSelectedFolder { get; set; }

        public string SelectFolder() => NextSelectedFolder;
        public string SelectFolder(string initialPath) => NextSelectedFolder;
        public string SelectFile(string filter) => throw new NotImplementedException();
        public string SelectFile(string filter, string initialDirectory) => throw new NotImplementedException();
        public List<string> SelectFiles(string filter) => throw new NotImplementedException();
        public List<string> SelectFiles(string filter, string initialDirectory) => throw new NotImplementedException();
        public string SelectIconFile() => throw new NotImplementedException();
        public string SelectIconFile(string initialDirectory) => throw new NotImplementedException();
        public string SelectImagefile() => NextSelectedImageFile;
        public string SelectImagefile(string initialDirectory) => NextSelectedImageFile;
        public string SaveFile(string filter) => throw new NotImplementedException();
        public string SaveFile(string filter, string initialDirectory) => throw new NotImplementedException();
        public string SaveFile(string filter, bool promptOverwrite) => throw new NotImplementedException();
        public string SaveFile(string filter, bool promptOverwrite, string initialDirectory) => throw new NotImplementedException();
        public StringSelectionDialogResult SelectString(string messageBoxText, string caption, string initialValue) => throw new NotImplementedException();
        public StringSelectionDialogResult SelectString(string messageBoxText, string caption, string initialValue, List<MessageBoxToggle> toggles) => throw new NotImplementedException();
        public void ShowSelectableString(string messageBoxText, string caption, string selectableText) => throw new NotImplementedException();
        public ImageFileOption ChooseImageFile(List<ImageFileOption> files, string caption, double width, double height) => throw new NotImplementedException();
        public GenericItemOption ChooseItemWithSearch(List<GenericItemOption> items, Func<string, List<GenericItemOption>> searchFunction, string defaultSearch, string caption) => throw new NotImplementedException();
        public GlobalProgressResult ActivateGlobalProgress(Action<GlobalProgressActionArgs> progresAction, GlobalProgressOptions progressOptions) => throw new NotImplementedException();
        public GlobalProgressResult ActivateGlobalProgress(Func<GlobalProgressActionArgs, System.Threading.Tasks.Task> progresAction, GlobalProgressOptions progressOptions) => throw new NotImplementedException();
        public Window CreateWindow(WindowCreationOptions options) => throw new NotImplementedException();
        public Window GetCurrentAppWindow() => throw new NotImplementedException();
    }
}
