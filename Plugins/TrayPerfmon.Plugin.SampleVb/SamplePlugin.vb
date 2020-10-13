Imports System.ComponentModel.Plugin
Imports System.Drawing
Imports System.Drawing.Drawing2D

<Plugin("SamplePluginVb", "Sample Plugin: Memory and Paging File")>
Public Class SamplePlugin
    Inherits NotifyIconPlugin

    Protected Overrides ReadOnly Property Factories As Lazy(Of PerformanceCounter)()
        Get
            Return _factories
        End Get
    End Property

    Sub New()
        MyBase.New(2, 1000 / 15)
    End Sub

    Protected Overrides Sub Clear(graphics As Graphics)
        ' Clear icon image
        graphics.Clear(Color.Transparent)
    End Sub

    Protected Overrides Sub Draw(graphics As Graphics, value() As Single)
        graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim bounds = graphics.VisibleClipBounds
        ' Draw Memory and Paging File
        Const row = 2, column = 4
        Const count = row * column, sample = 100 / count
        Dim x = bounds.X, y = bounds.Y + bounds.Height / 2.0F
        Dim width = bounds.Width / column, height = bounds.Height / 2.0F / row
        For rw = 0 To 1
            Dim [next] = CInt(value(rw) + sample - 0.1F) / sample
            For i = 0 To count
                Dim brush = IIf(i < [next], _brushes(rw), Brushes.Black)
                Dim dx = i Mod column
                Dim dy = i \ column
                graphics.FillRectangle(brush, x + width * dx, y * rw + height * dy, width, height)
            Next
        Next
    End Sub

    Shared Sub New()
        ' Open 'Server Explorer' window and find your own performance counter in local machine
        _factories = New Lazy(Of PerformanceCounter)() {
            New Lazy(Of PerformanceCounter)(Function() New PerformanceCounter("Memory", "% Committed Bytes In Use", True)),
            New Lazy(Of PerformanceCounter)(Function() New PerformanceCounter("Paging File", "% Usage", "_Total", True))
        }
        ' Create Read/Write color brush
        _brushes = New Brush() {Brushes.Cyan, Brushes.Magenta}

    End Sub

    Shared _factories As Lazy(Of PerformanceCounter)()
    Shared _brushes As Brush()

End Class
