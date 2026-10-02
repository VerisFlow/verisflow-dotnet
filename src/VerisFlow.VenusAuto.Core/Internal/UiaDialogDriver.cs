// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Interop.UIAutomationClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <inheritdoc />
internal sealed class UiaDialogDriver : IUiaDialogDriver
{
    private const int UiaInvokePatternId = 10000;
    private const int UiaValuePatternId = 10002;
    private const int UiaSelectionItemPatternId = 10010;
    private const int UiaTogglePatternId = 10015;

    private const int UiaButtonControlTypeId = 50000;
    private const int UiaCheckBoxControlTypeId = 50002;
    private const int UiaComboBoxControlTypeId = 50003;
    private const int UiaEditControlTypeId = 50004;
    private const int UiaRadioButtonControlTypeId = 50013;
    private const int UiaTextControlTypeId = 50020;

    private readonly IWindowMessenger _messenger;
    private readonly Lazy<IUIAutomation?> _automation;

    public UiaDialogDriver(IWindowMessenger messenger)
    {
        _messenger = messenger;
        _automation = new Lazy<IUIAutomation?>(InitializeAutomation, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsCustomDialog(IntPtr hwnd, int processId, IntPtr mainWindow)
    {
        if (hwnd == IntPtr.Zero || hwnd == mainWindow)
        {
            return false;
        }

        if (!_messenger.IsWindow(hwnd) || !_messenger.IsWindowVisible(hwnd) || _messenger.GetProcessId(hwnd) != processId)
        {
            return false;
        }

        if (string.Equals(_messenger.GetClassName(hwnd), NativeMethods.DialogClassName, StringComparison.Ordinal))
        {
            return false;
        }

        var uia = _automation.Value;
        if (uia == null)
        {
            return false;
        }

        try
        {
            var root = uia.ElementFromHandle(hwnd);
            if (root == null) return false;

            try
            {
                var trueCond = uia.CreateTrueCondition();
                var descendants = root.FindAll(TreeScope.TreeScope_Descendants, trueCond);
                if (descendants == null) return false;

                try
                {
                    for (int i = 0; i < descendants.Length; i++)
                    {
                        var child = descendants.GetElement(i);
                        try
                        {
                            int type = child.CurrentControlType;
                            if (type == UiaButtonControlTypeId || type == UiaEditControlTypeId)
                            {
                                return true;
                            }
                        }
                        finally
                        {
                            SafeRelease(child);
                        }
                    }
                }
                finally
                {
                    SafeRelease(descendants);
                }
            }
            finally
            {
                SafeRelease(root);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public VenusDialogInfo? CaptureCustomDialog(IntPtr hwnd, IntPtr mainWindow, int processId)
    {
        var uia = _automation.Value;
        if (uia == null || hwnd == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var root = uia.ElementFromHandle(hwnd);
            if (root == null) return null;

            try
            {
                string title = root.CurrentName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = _messenger.GetText(hwnd).Trim();
                }

                var messageLines = new List<string>();
                var buttons = new List<VenusDialogButton>();
                var options = new List<VenusDialogOption>();
                var inputs = new List<VenusDialogInput>();
                var dropdowns = new List<VenusDialogDropdown>();

                var trueCond = uia.CreateTrueCondition();
                var descendants = root.FindAll(TreeScope.TreeScope_Descendants, trueCond);

                if (descendants != null)
                {
                    try
                    {
                        for (int i = 0; i < descendants.Length; i++)
                        {
                            var child = descendants.GetElement(i);
                            try
                            {
                                int controlType = child.CurrentControlType;
                                string name = (child.CurrentName ?? string.Empty).Trim();
                                string autoId = child.CurrentAutomationId ?? string.Empty;
                                bool enabled = child.CurrentIsEnabled == 1;

                                if (controlType == UiaTextControlTypeId)
                                {
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        messageLines.Add(name);
                                    }
                                }
                                else if (controlType == UiaEditControlTypeId)
                                {
                                    string val = string.Empty;
                                    bool isReadOnly = false;

                                    if (child.GetCurrentPattern(UiaValuePatternId) is IUIAutomationValuePattern valPattern)
                                    {
                                        val = valPattern.CurrentValue ?? string.Empty;
                                        isReadOnly = valPattern.CurrentIsReadOnly == 1;
                                    }

                                    if (isReadOnly)
                                    {
                                        if (!string.IsNullOrEmpty(val)) messageLines.Add(val);
                                    }
                                    else
                                    {
                                        string key = !string.IsNullOrEmpty(autoId) ? autoId : $"Edit_{inputs.Count}";
                                        inputs.Add(new VenusDialogInput(key, name, val, false));
                                        if (!string.IsNullOrEmpty(val)) messageLines.Add(val);
                                    }
                                }
                                else if (controlType == UiaButtonControlTypeId)
                                {
                                    int id = GenerateButtonId(name, autoId, buttons.Count);
                                    bool isDefault = id == 1 || name.Equals("OK", StringComparison.OrdinalIgnoreCase);
                                    buttons.Add(new VenusDialogButton(id, name, enabled, isDefault));
                                }
                                else if (controlType == UiaCheckBoxControlTypeId)
                                {
                                    bool isChecked = false;
                                    if (child.GetCurrentPattern(UiaTogglePatternId) is IUIAutomationTogglePattern toggle)
                                    {
                                        isChecked = toggle.CurrentToggleState == ToggleState.ToggleState_On;
                                    }
                                    int id = GenerateOptionId(name, autoId, options.Count);
                                    options.Add(new VenusDialogOption(id, name, isChecked, enabled));
                                }
                                else if (controlType == UiaRadioButtonControlTypeId)
                                {
                                    bool isSelected = false;
                                    if (child.GetCurrentPattern(UiaSelectionItemPatternId) is IUIAutomationSelectionItemPattern sel)
                                    {
                                        isSelected = sel.CurrentIsSelected == 1;
                                    }
                                    int id = GenerateOptionId(name, autoId, options.Count);
                                    options.Add(new VenusDialogOption(id, name, isSelected, enabled));
                                }
                                else if (controlType == UiaComboBoxControlTypeId)
                                {
                                    string selected = string.Empty;
                                    if (child.GetCurrentPattern(UiaValuePatternId) is IUIAutomationValuePattern cVal)
                                    {
                                        selected = cVal.CurrentValue ?? string.Empty;
                                    }
                                    string key = !string.IsNullOrEmpty(autoId) ? autoId : $"Combo_{dropdowns.Count}";
                                    dropdowns.Add(new VenusDialogDropdown(key, name, selected, Array.Empty<string>()));
                                }
                            }
                            finally
                            {
                                SafeRelease(child);
                            }
                        }
                    }
                    finally
                    {
                        SafeRelease(descendants);
                    }
                }

                var owner = _messenger.GetOwner(hwnd);
                var blocksMainWindow = mainWindow != IntPtr.Zero
                    && (owner == mainWindow || !_messenger.IsWindowEnabled(mainWindow));

                var fingerprint = ComputeFingerprint(hwnd, title, buttons.Select(b => b.Id).Concat(options.Select(o => o.Id)));

                return new VenusDialogInfo(
                    hwnd.ToInt64(),
                    owner.ToInt64(),
                    VenusDialogKind.Custom,
                    title,
                    string.Join("\n", messageLines),
                    buttons,
                    options,
                    blocksMainWindow,
                    fingerprint)
                {
                    Inputs = inputs,
                    Dropdowns = dropdowns,
                    IsCustom = true
                };
            }
            finally
            {
                SafeRelease(root);
            }
        }
        catch
        {
            return null;
        }
    }

    public bool ClickButton(IntPtr hwnd, int buttonId)
    {
        var uia = _automation.Value;
        if (uia == null || hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var root = uia.ElementFromHandle(hwnd);
            if (root == null) return false;

            try
            {
                var trueCond = uia.CreateTrueCondition();
                var descendants = root.FindAll(TreeScope.TreeScope_Descendants, trueCond);
                if (descendants == null) return false;

                try
                {
                    int buttonIndex = 0;
                    for (int i = 0; i < descendants.Length; i++)
                    {
                        var child = descendants.GetElement(i);
                        try
                        {
                            if (child.CurrentControlType == UiaButtonControlTypeId)
                            {
                                string name = (child.CurrentName ?? string.Empty).Trim();
                                string autoId = child.CurrentAutomationId ?? string.Empty;
                                int computedId = GenerateButtonId(name, autoId, buttonIndex);

                                if (computedId == buttonId || (buttonId == 1 && name.Equals("OK", StringComparison.OrdinalIgnoreCase)) || (buttonId == 2 && name.Equals("Cancel", StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (child.GetCurrentPattern(UiaInvokePatternId) is IUIAutomationInvokePattern invokePattern)
                                    {
                                        invokePattern.Invoke();
                                        return true;
                                    }
                                }

                                buttonIndex++;
                            }
                        }
                        finally
                        {
                            SafeRelease(child);
                        }
                    }
                }
                finally
                {
                    SafeRelease(descendants);
                }
            }
            finally
            {
                SafeRelease(root);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public bool Submit(IntPtr hwnd, VenusDialogSubmission submission)
    {
        if (hwnd == IntPtr.Zero || submission == null)
        {
            return false;
        }

        var uia = _automation.Value;
        if (uia == null)
        {
            return false;
        }

        try
        {
            var root = uia.ElementFromHandle(hwnd);
            if (root == null) return false;

            try
            {
                var trueCond = uia.CreateTrueCondition();
                var descendants = root.FindAll(TreeScope.TreeScope_Descendants, trueCond);
                if (descendants != null)
                {
                    try
                    {
                        // 1. Process text inputs
                        if (submission.Inputs != null && submission.Inputs.Count > 0)
                        {
                            int editIndex = 0;
                            for (int i = 0; i < descendants.Length; i++)
                            {
                                var child = descendants.GetElement(i);
                                try
                                {
                                    if (child.CurrentControlType == UiaEditControlTypeId)
                                    {
                                        string autoId = child.CurrentAutomationId ?? string.Empty;
                                        string name = child.CurrentName ?? string.Empty;
                                        string key = $"Edit_{editIndex}";

                                        string? targetVal = null;
                                        if (!string.IsNullOrEmpty(autoId) && submission.Inputs.TryGetValue(autoId, out var v1))
                                        {
                                            targetVal = v1;
                                        }
                                        else if (!string.IsNullOrEmpty(name) && submission.Inputs.TryGetValue(name, out var v2))
                                        {
                                            targetVal = v2;
                                        }
                                        else if (submission.Inputs.TryGetValue(key, out var v3))
                                        {
                                            targetVal = v3;
                                        }
                                        else if (editIndex == 0)
                                        {
                                            if (submission.Inputs.TryGetValue("0", out var v4))
                                            {
                                                targetVal = v4;
                                            }
                                            else if (submission.Inputs.TryGetValue("Path", out var v5))
                                            {
                                                targetVal = v5;
                                            }
                                        }

                                        if (targetVal != null)
                                        {
                                            if (child.GetCurrentPattern(UiaValuePatternId) is IUIAutomationValuePattern valPattern)
                                            {
                                                valPattern.SetValue(targetVal);
                                            }
                                        }

                                        editIndex++;
                                    }
                                }
                                finally
                                {
                                    SafeRelease(child);
                                }
                            }
                        }

                        // 2. Process check boxes and radio buttons
                        if (submission.Options != null && submission.Options.Count > 0)
                        {
                            int optIndex = 0;
                            for (int i = 0; i < descendants.Length; i++)
                            {
                                var child = descendants.GetElement(i);
                                try
                                {
                                    int cType = child.CurrentControlType;
                                    if (cType == UiaCheckBoxControlTypeId || cType == UiaRadioButtonControlTypeId)
                                    {
                                        string autoId = child.CurrentAutomationId ?? string.Empty;
                                        string name = child.CurrentName ?? string.Empty;
                                        string key = $"Opt_{optIndex}";

                                        bool? targetState = null;
                                        if (!string.IsNullOrEmpty(autoId) && submission.Options.TryGetValue(autoId, out var s1)) targetState = s1;
                                        else if (!string.IsNullOrEmpty(name) && submission.Options.TryGetValue(name, out var s2)) targetState = s2;
                                        else if (submission.Options.TryGetValue(key, out var s3)) targetState = s3;

                                        if (targetState.HasValue)
                                        {
                                            if (cType == UiaCheckBoxControlTypeId && child.GetCurrentPattern(UiaTogglePatternId) is IUIAutomationTogglePattern toggle)
                                            {
                                                bool isCurrentlyOn = toggle.CurrentToggleState == ToggleState.ToggleState_On;
                                                if (isCurrentlyOn != targetState.Value) toggle.Toggle();
                                            }
                                            else if (cType == UiaRadioButtonControlTypeId && child.GetCurrentPattern(UiaSelectionItemPatternId) is IUIAutomationSelectionItemPattern sel)
                                            {
                                                if (targetState.Value) sel.Select();
                                            }
                                        }

                                        optIndex++;
                                    }
                                }
                                finally
                                {
                                    SafeRelease(child);
                                }
                            }
                        }

                        // 3. Process dropdowns
                        if (submission.Dropdowns != null && submission.Dropdowns.Count > 0)
                        {
                            int comboIndex = 0;
                            for (int i = 0; i < descendants.Length; i++)
                            {
                                var child = descendants.GetElement(i);
                                try
                                {
                                    if (child.CurrentControlType == UiaComboBoxControlTypeId)
                                    {
                                        string autoId = child.CurrentAutomationId ?? string.Empty;
                                        string name = child.CurrentName ?? string.Empty;
                                        string key = $"Combo_{comboIndex}";

                                        string? targetOption = null;
                                        if (!string.IsNullOrEmpty(autoId) && submission.Dropdowns.TryGetValue(autoId, out var o1)) targetOption = o1;
                                        else if (!string.IsNullOrEmpty(name) && submission.Dropdowns.TryGetValue(name, out var o2)) targetOption = o2;
                                        else if (submission.Dropdowns.TryGetValue(key, out var o3)) targetOption = o3;

                                        if (targetOption != null && child.GetCurrentPattern(UiaValuePatternId) is IUIAutomationValuePattern valPattern)
                                        {
                                            valPattern.SetValue(targetOption);
                                        }

                                        comboIndex++;
                                    }
                                }
                                finally
                                {
                                    SafeRelease(child);
                                }
                            }
                        }
                    }
                    finally
                    {
                        SafeRelease(descendants);
                    }
                }

                // 4. Trigger target button action
                if (submission.ButtonId.HasValue)
                {
                    return ClickButton(hwnd, submission.ButtonId.Value);
                }

                if (!string.IsNullOrEmpty(submission.ButtonName))
                {
                    return ClickButtonByName(hwnd, submission.ButtonName!);
                }

                return true;
            }
            finally
            {
                SafeRelease(root);
            }
        }
        catch
        {
            return false;
        }
    }

    private bool ClickButtonByName(IntPtr hwnd, string buttonName)
    {
        var uia = _automation.Value;
        if (uia == null || hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var root = uia.ElementFromHandle(hwnd);
            if (root == null) return false;

            try
            {
                var trueCond = uia.CreateTrueCondition();
                var descendants = root.FindAll(TreeScope.TreeScope_Descendants, trueCond);
                if (descendants == null) return false;

                try
                {
                    for (int i = 0; i < descendants.Length; i++)
                    {
                        var child = descendants.GetElement(i);
                        try
                        {
                            if (child.CurrentControlType == UiaButtonControlTypeId)
                            {
                                string name = child.CurrentName ?? string.Empty;
                                if (string.Equals(name.Trim(), buttonName.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    if (child.GetCurrentPattern(UiaInvokePatternId) is IUIAutomationInvokePattern inv)
                                    {
                                        inv.Invoke();
                                        return true;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            SafeRelease(child);
                        }
                    }
                }
                finally
                {
                    SafeRelease(descendants);
                }
            }
            finally
            {
                SafeRelease(root);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static int GenerateButtonId(string text, string autoId, int index)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (normalized == "ok" || normalized == "确定" || normalized == "yes" || normalized == "是")
        {
            return 1;
        }
        if (normalized == "cancel" || normalized == "取消" || normalized == "no" || normalized == "否")
        {
            return 2;
        }

        var rawKey = !string.IsNullOrEmpty(autoId) ? autoId : (!string.IsNullOrEmpty(text) ? text : $"Btn_{index}");
        int hash = 0;
        foreach (char c in rawKey)
        {
            hash = (hash * 31) + c;
        }
        int id = -Math.Abs(hash);
        return id == 0 ? -(index + 100) : id;
    }

    private static int GenerateOptionId(string text, string autoId, int index)
    {
        var rawKey = !string.IsNullOrEmpty(autoId) ? autoId : (!string.IsNullOrEmpty(text) ? text : $"Opt_{index}");
        int hash = 0;
        foreach (char c in rawKey)
        {
            hash = (hash * 31) + c;
        }
        int id = -Math.Abs(hash) - 1000;
        return id == 0 ? -(index + 2000) : id;
    }

    private static string ComputeFingerprint(IntPtr dialog, string title, IEnumerable<int> controlIds)
    {
        var source = dialog.ToInt64().ToString(CultureInfo.InvariantCulture)
            + "|" + title
            + "|" + string.Join(",", controlIds.OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture)));

        var bytes = Encoding.UTF8.GetBytes(source);

#if NET5_0_OR_GREATER
        return Convert.ToHexString(SHA256.HashData(bytes), 0, 8);
#else
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty);
        }
#endif
    }

    private static void SafeRelease(object? comObj)
    {
#if NET5_0_OR_GREATER
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif
        if (comObj != null && Marshal.IsComObject(comObj))
        {
            try
            {
                Marshal.ReleaseComObject(comObj);
            }
            catch
            {
            }
        }
    }

    private static IUIAutomation? InitializeAutomation()
    {
#if NET5_0_OR_GREATER
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
#endif
        try
        {
            return new CUIAutomation8();
        }
        catch
        {
            try
            {
                return new CUIAutomation();
            }
            catch
            {
                return null;
            }
        }
    }
}
