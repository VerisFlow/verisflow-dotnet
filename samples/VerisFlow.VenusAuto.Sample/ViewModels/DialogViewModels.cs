using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Sample.ViewModels
{
    /// <summary>
    /// Represents an editable or viewable input field of a dialog.
    /// </summary>
    public sealed class DialogInputViewModel : INotifyPropertyChanged
    {
        private string _text;

        public DialogInputViewModel(VenusDialogInput input)
        {
            Key = input.Key;
            Label = input.Label;
            _text = input.Value;
            IsReadOnly = input.IsReadOnly;
        }

        public string Key { get; }

        public string Label { get; }

        public bool IsReadOnly { get; }

        public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Key : $"{Label} ({Key})";

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// One open Run Control dialog as shown in the Dialogs panel.
    /// </summary>
    public sealed class DialogItemViewModel
    {
        public DialogItemViewModel(VenusDialogInfo info, MainViewModel owner)
        {
            Info = info;
            Inputs = info.Inputs.Select(input => new DialogInputViewModel(input)).ToList();
            Buttons = info.Buttons.Select(button => new DialogButtonViewModel(info, button, owner)).ToList();
            Options = info.Options
                .Select(option => $"{(option.Checked ? "[x]" : "[ ]")} {option.Text}  (ID {option.Id}{(option.Enabled ? string.Empty : ", disabled")})")
                .ToList();

            SubmitCommand = new RelayCommand(async _ =>
            {
                var submission = new VenusDialogSubmission
                {
                    ButtonId = 1
                };

                foreach (var input in Inputs)
                {
                    submission.Inputs[input.Key] = input.Text;
                }

                await owner.SubmitAsync(info, submission);
            }, _ => !owner.IsBusy);
        }

        public VenusDialogInfo Info { get; }

        public string Header => $"{Info.Kind}{(Info.IsCustom ? " [Custom UIA]" : string.Empty)} · {(string.IsNullOrEmpty(Info.Title) ? "(no title)" : Info.Title)}";

        public string Details =>
            $"HWND 0x{Info.Handle:X} · owner 0x{Info.OwnerHandle:X} · {(Info.BlocksMainWindow ? "blocks Run Control" : "does not block")} · fingerprint {Info.Fingerprint}";

        public string Message => string.IsNullOrEmpty(Info.Message) ? "(no text)" : Info.Message;

        public IReadOnlyList<DialogInputViewModel> Inputs { get; }

        public bool HasInputs => Inputs.Count > 0;

        public IReadOnlyList<DialogButtonViewModel> Buttons { get; }

        public IReadOnlyList<string> Options { get; }

        public bool HasOptions => Options.Count > 0;

        public ICommand SubmitCommand { get; }
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
