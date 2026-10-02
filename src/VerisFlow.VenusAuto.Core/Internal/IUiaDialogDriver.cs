// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// Probes and manipulates non-standard dialog windows using Microsoft UI Automation.
/// </summary>
internal interface IUiaDialogDriver
{
    /// <summary>Determines whether the specified window handle represents a custom UI Automation dialog.</summary>
    bool IsCustomDialog(IntPtr hwnd, int processId, IntPtr mainWindow);

    /// <summary>Extracts structured UI tree metadata from a custom dialog.</summary>
    VenusDialogInfo? CaptureCustomDialog(IntPtr hwnd, IntPtr mainWindow, int processId);

    /// <summary>Invokes a button element on a custom dialog by its identifier.</summary>
    bool ClickButton(IntPtr hwnd, int buttonId);

    /// <summary>Applies input values, options, dropdown selections, and optionally triggers a button.</summary>
    bool Submit(IntPtr hwnd, VenusDialogSubmission submission);
}
