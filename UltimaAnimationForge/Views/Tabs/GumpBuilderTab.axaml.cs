using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using UltimaAnimationForge.Models;
using UltimaAnimationForge.ViewModels;

namespace UltimaAnimationForge.Views.Tabs;

public partial class GumpBuilderTab : UserControl
{
    private GumpBuilderElement? draggedGumpBuilderElement;
    private Point gumpBuilderDragStartPointer;
    private int gumpBuilderDragStartX;
    private int gumpBuilderDragStartY;
    private GumpBuilderElement? resizingGumpBuilderElement;
    private string gumpBuilderResizeHandle = string.Empty;
    private Point gumpBuilderResizeStartPointer;
    private int gumpBuilderResizeStartX;
    private int gumpBuilderResizeStartY;
    private int gumpBuilderResizeStartWidth;
    private int gumpBuilderResizeStartHeight;

    public GumpBuilderTab()
    {
        InitializeComponent();
    }

    private void GumpBuilderElement_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (control.DataContext is not GumpBuilderElement element)
        {
            return;
        }

        draggedGumpBuilderElement = element;
        gumpBuilderDragStartX = element.X;
        gumpBuilderDragStartY = element.Y;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedGumpBuilderElement = element;
        }

        Control? canvas = control.FindAncestorOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        gumpBuilderDragStartPointer = e.GetPosition(canvas);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void GumpBuilderElement_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (draggedGumpBuilderElement == null)
        {
            return;
        }

        if (sender is not Control control)
        {
            return;
        }

        Control? canvas = control.FindAncestorOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Point currentPointer = e.GetPosition(canvas);

        int deltaX = (int)Math.Round(currentPointer.X - gumpBuilderDragStartPointer.X);
        int deltaY = (int)Math.Round(currentPointer.Y - gumpBuilderDragStartPointer.Y);

        if (DataContext is MainWindowViewModel viewModel)
        {
            draggedGumpBuilderElement.X = viewModel.SnapGumpBuilderValue(gumpBuilderDragStartX + deltaX);
            draggedGumpBuilderElement.Y = viewModel.SnapGumpBuilderValue(gumpBuilderDragStartY + deltaY);
        }
        else
        {
            draggedGumpBuilderElement.X = gumpBuilderDragStartX + deltaX;
            draggedGumpBuilderElement.Y = gumpBuilderDragStartY + deltaY;
        }

        e.Handled = true;
    }

    private void GumpBuilderElement_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control control)
        {
            e.Pointer.Capture(null);
        }

        draggedGumpBuilderElement = null;
        e.Handled = true;
    }

    private void GumpBuilderResizeHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (control.DataContext is not GumpBuilderElement element)
        {
            return;
        }

        resizingGumpBuilderElement = element;
        gumpBuilderResizeHandle = control.Tag?.ToString() ?? string.Empty;

        gumpBuilderResizeStartX = element.X;
        gumpBuilderResizeStartY = element.Y;
        gumpBuilderResizeStartWidth = element.Width;
        gumpBuilderResizeStartHeight = element.Height;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedGumpBuilderElement = element;
        }

        Control? canvas = control.FindAncestorOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        gumpBuilderResizeStartPointer = e.GetPosition(canvas);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void GumpBuilderResizeHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (resizingGumpBuilderElement == null)
        {
            return;
        }

        if (sender is not Control control)
        {
            return;
        }

        Control? canvas = control.FindAncestorOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Point currentPointer = e.GetPosition(canvas);

        int deltaX = (int)Math.Round(currentPointer.X - gumpBuilderResizeStartPointer.X);
        int deltaY = (int)Math.Round(currentPointer.Y - gumpBuilderResizeStartPointer.Y);

        int newX = gumpBuilderResizeStartX;
        int newY = gumpBuilderResizeStartY;
        int newWidth = gumpBuilderResizeStartWidth;
        int newHeight = gumpBuilderResizeStartHeight;

        if (gumpBuilderResizeHandle.Contains("W"))
        {
            newX = gumpBuilderResizeStartX + deltaX;
            newWidth = gumpBuilderResizeStartWidth - deltaX;
        }

        if (gumpBuilderResizeHandle.Contains("E"))
        {
            newWidth = gumpBuilderResizeStartWidth + deltaX;
        }

        if (gumpBuilderResizeHandle.Contains("N"))
        {
            newY = gumpBuilderResizeStartY + deltaY;
            newHeight = gumpBuilderResizeStartHeight - deltaY;
        }

        if (gumpBuilderResizeHandle.Contains("S"))
        {
            newHeight = gumpBuilderResizeStartHeight + deltaY;
        }

        const int minSize = 8;

        if (newWidth < minSize)
        {
            if (gumpBuilderResizeHandle.Contains("W"))
            {
                newX = gumpBuilderResizeStartX + gumpBuilderResizeStartWidth - minSize;
            }

            newWidth = minSize;
        }

        if (newHeight < minSize)
        {
            if (gumpBuilderResizeHandle.Contains("N"))
            {
                newY = gumpBuilderResizeStartY + gumpBuilderResizeStartHeight - minSize;
            }

            newHeight = minSize;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            newX = viewModel.SnapGumpBuilderValue(newX);
            newY = viewModel.SnapGumpBuilderValue(newY);
            newWidth = viewModel.SnapGumpBuilderValue(newWidth);
            newHeight = viewModel.SnapGumpBuilderValue(newHeight);
        }

        resizingGumpBuilderElement.X = newX;
        resizingGumpBuilderElement.Y = newY;
        resizingGumpBuilderElement.Width = newWidth;
        resizingGumpBuilderElement.Height = newHeight;

        e.Handled = true;
    }

    private void GumpBuilderResizeHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        resizingGumpBuilderElement = null;
        gumpBuilderResizeHandle = string.Empty;

        e.Handled = true;
    }
}