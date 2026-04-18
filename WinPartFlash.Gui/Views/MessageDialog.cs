using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using WinPartFlash.Gui.Resources;

namespace WinPartFlash.Gui.Views;

public enum MessageDialogKind
{
    Info,
    Error,
    Confirm,
}

public sealed class MessageDialog : Window
{
    private bool _result;

    private MessageDialog(string title, string body, MessageDialogKind kind)
    {
        Title = title;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        MinHeight = 180;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (IBrush?)Application.Current!.FindResource("PureWhite");

        var kindLabel = new TextBlock
        {
            Classes = { "SectionLabel" },
            Text = kind switch
            {
                MessageDialogKind.Error => "ERROR",
                MessageDialogKind.Confirm => "CONFIRM",
                _ => "INFO",
            },
        };

        var titleBlock = new TextBlock
        {
            Classes = { "SubDisplay" },
            Text = title,
            TextWrapping = TextWrapping.Wrap,
        };

        var bodyBlock = new SelectableTextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 20,
            Foreground = (IBrush?)Application.Current!.FindResource("NearBlack"),
        };

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        if (kind == MessageDialogKind.Confirm)
        {
            var cancelButton = new Button { Content = Strings.DialogButtonCancel };
            cancelButton.Click += (_, _) =>
            {
                _result = false;
                Close();
            };

            var confirmButton = new Button { Content = Strings.DialogButtonConfirm };
            confirmButton.Classes.Add("Primary");
            confirmButton.Click += (_, _) =>
            {
                _result = true;
                Close();
            };

            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(confirmButton);
        }
        else
        {
            var okButton = new Button { Content = Strings.DialogButtonOk };
            okButton.Classes.Add("Primary");
            okButton.Click += (_, _) =>
            {
                _result = true;
                Close();
            };
            buttonRow.Children.Add(okButton);
        }

        var stack = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                kindLabel,
                titleBlock,
                bodyBlock,
                buttonRow,
            },
        };

        var card = new Border
        {
            Classes = { "Card" },
            Margin = new(20),
            Child = stack,
        };

        Content = card;
    }

    public static async Task<bool> ShowAsync(Window? owner, string title, string body, MessageDialogKind kind)
    {
        var dialog = new MessageDialog(title, body, kind);
        owner ??= (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.Windows.FirstOrDefault(w => w.IsActive)
            ?? (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner != null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            var tcs = new TaskCompletionSource();
            dialog.Closed += (_, _) => tcs.TrySetResult();
            dialog.Show();
            await tcs.Task;
        }
        return dialog._result;
    }

    public static Task ShowInfoAsync(Window? owner, string title, string body)
        => ShowAsync(owner, title, body, MessageDialogKind.Info);

    public static Task ShowErrorAsync(Window? owner, string title, string body)
        => ShowAsync(owner, title, body, MessageDialogKind.Error);

    public static Task<bool> ConfirmAsync(Window? owner, string title, string body)
        => ShowAsync(owner, title, body, MessageDialogKind.Confirm);
}
