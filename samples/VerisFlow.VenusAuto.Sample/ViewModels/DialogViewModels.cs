using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Sample.ViewModels
{
    /// <summary>
    /// One open Run Control dialog as shown in the Dialogs panel.
    /// </summary>
    public sealed class DialogItemViewModel
    {
        public DialogItemViewModel(VenusDialogInfo info, MainViewModel owner)
        {
            Info = info;
            Buttons = info.Buttons.Select(button => new DialogButtonViewModel(info, button, owner)).ToList();
            Options = info.Options
                .Select(option => $"{(option.Checked ? "[x]" : "[ ]")} {option.Text}  (ID {option.Id}{(option.Enabled ? string.Empty : ", disabled")})")
                .ToList();
        }

        public VenusDialogInfo Info { get; }

        public string Header => $"{Info.Kind} · {(string.IsNullOrEmpty(Info.Title) ? "(no title)" : Info.Title)}";

        public string Details =>
            $"HWND 0x{Info.Handle:X} · owner 0x{Info.OwnerHandle:X} · {(Info.BlocksMainWindow ? "blocks Run Control" : "does not block")} · fingerprint {Info.Fingerprint}";

        public string Message => string.IsNullOrEmpty(Info.Message) ? "(no text)" : Info.Message;

        public IReadOnlyList<DialogButtonViewModel> Buttons { get; }

        public IReadOnlyList<string> Options { get; }

        public bool HasOptions => Options.Count > 0;
    }

    /// <summary>
    /// A dialog button; pressing it responds through the library (fingerprint checked).
    /// </summary>
    public sealed class DialogButtonViewModel
    {
        private readonly VenusDialogButton _button;

        public DialogButtonViewModel(VenusDialogInfo dialog, VenusDialogButton button, MainViewModel owner)
        {
            _button = button;
            Command = new RelayCommand(
                async _ => await owner.RespondAsync(dialog, button),
                _ => button.Enabled && !owner.IsBusy);
        }

        public string Label =>
            $"{(_button.IsDefault ? "★ " : string.Empty)}{(string.IsNullOrEmpty(_button.Text) ? "(no text)" : _button.Text)}  [{_button.Id}]";

        public string ToolTip => _button.Enabled
            ? $"Press button ID {_button.Id} through RespondToDialogAsync."
            : "This button is disabled in Run Control.";

        public ICommand Command { get; }
    }
}