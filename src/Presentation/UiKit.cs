using System;
using Godot;

namespace LandBuilder.Presentation;

/// <summary>Theme, fonts and small widget factories so every screen shares one soft, paper-like look.</summary>
public static class UiKit
{
    private static Font? _regular;
    private static Font? _bold;
    private static Font? _black;

    public static Font Regular => _regular ??= MakeFont(500);
    public static Font Bold => _bold ??= MakeFont(700);
    public static Font Black => _black ??= MakeFont(900);

    private static Font MakeFont(int weight) => new SystemFont
    {
        FontNames = ["Nunito", "Quicksand", "Segoe UI Variable Display", "Segoe UI", "Helvetica Neue", "Arial"],
        FontWeight = weight,
        Antialiasing = TextServer.FontAntialiasing.Gray,
        SubpixelPositioning = TextServer.SubpixelPositioning.Auto,
        AllowSystemFallback = true
    };

    public static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFont = Regular, DefaultFontSize = 18 };

        theme.SetColor("font_color", "Label", Palette.Ink);
        theme.SetColor("font_color", "Button", Palette.Ink);
        theme.SetColor("font_hover_color", "Button", Palette.Ink);
        theme.SetColor("font_pressed_color", "Button", Palette.Ink);
        theme.SetColor("font_focus_color", "Button", Palette.Ink);
        theme.SetColor("font_disabled_color", "Button", new Color(Palette.Ink, 0.35f));
        theme.SetFont("font", "Button", Bold);
        theme.SetFontSize("font_size", "Button", 20);

        theme.SetStylebox("normal", "Button", Box(Palette.Paper, 14, border: Palette.PaperShade.Darkened(0.05f), borderWidth: 2, shadow: 4));
        theme.SetStylebox("hover", "Button", Box(Palette.Paper.Lightened(0.4f), 14, border: Palette.Accent, borderWidth: 2, shadow: 6));
        theme.SetStylebox("pressed", "Button", Box(Palette.PaperShade, 14, border: Palette.AccentDeep, borderWidth: 2, shadow: 1));
        theme.SetStylebox("disabled", "Button", Box(new Color(Palette.Paper, 0.6f), 14, border: new Color(Palette.PaperShade, 0.6f), borderWidth: 2));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());

        theme.SetStylebox("panel", "PanelContainer", Box(new Color(Palette.Paper, 0.94f), 18, shadow: 8));

        // Sliders & checkboxes
        theme.SetStylebox("slider", "HSlider", Box(Palette.PaperShade.Darkened(0.08f), 6, margin: 3));
        theme.SetStylebox("grabber_area", "HSlider", Box(Palette.Accent, 6, margin: 3));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Box(Palette.Accent.Lightened(0.1f), 6, margin: 3));
        theme.SetIcon("grabber", "HSlider", Dot(22, Palette.AccentDeep));
        theme.SetIcon("grabber_highlight", "HSlider", Dot(24, Palette.Accent));
        theme.SetColor("font_color", "CheckBox", Palette.Ink);
        theme.SetColor("font_hover_color", "CheckBox", Palette.Ink);
        theme.SetColor("font_pressed_color", "CheckBox", Palette.Ink);
        theme.SetColor("font_hover_pressed_color", "CheckBox", Palette.Ink);
        theme.SetIcon("checked", "CheckBox", Check(true));
        theme.SetIcon("unchecked", "CheckBox", Check(false));
        theme.SetStylebox("normal", "CheckBox", new StyleBoxEmpty());
        theme.SetStylebox("hover", "CheckBox", new StyleBoxEmpty());
        theme.SetStylebox("pressed", "CheckBox", new StyleBoxEmpty());
        theme.SetStylebox("hover_pressed", "CheckBox", new StyleBoxEmpty());
        theme.SetStylebox("focus", "CheckBox", new StyleBoxEmpty());

        theme.SetStylebox("background", "ProgressBar", Box(new Color(Palette.Ink, 0.12f), 6));
        theme.SetStylebox("fill", "ProgressBar", Box(Palette.Accent, 6));
        theme.SetColor("font_color", "ProgressBar", Colors.Transparent);
        return theme;
    }

    public static StyleBoxFlat Box(Color bg, int radius, Color? border = null, int borderWidth = 0, int shadow = 0, int margin = 0, int padH = 0, int padV = 0)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            CornerDetail = 8,
            AntiAliasing = true
        };
        box.SetCornerRadiusAll(radius);
        if (border is Color b && borderWidth > 0)
        {
            box.BorderColor = b;
            box.SetBorderWidthAll(borderWidth);
        }

        if (shadow > 0)
        {
            box.ShadowColor = new Color(0.08f, 0.12f, 0.16f, 0.22f);
            box.ShadowSize = shadow;
            box.ShadowOffset = new Vector2(0, shadow * 0.5f);
        }

        if (margin > 0)
            box.SetExpandMarginAll(-margin);

        box.ContentMarginLeft = padH;
        box.ContentMarginRight = padH;
        box.ContentMarginTop = padV;
        box.ContentMarginBottom = padV;
        return box;
    }

    public static Label Text(string text, int size, Color? color = null, Font? font = null, HorizontalAlignment align = HorizontalAlignment.Left, int outline = 0, Color? outlineColor = null)
    {
        var settings = new LabelSettings
        {
            Font = font ?? Regular,
            FontSize = size,
            FontColor = color ?? Palette.Ink,
            OutlineSize = outline,
            OutlineColor = outlineColor ?? Palette.Ink
        };
        return new Label
        {
            Text = text,
            LabelSettings = settings,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    public static PanelContainer Panel(Color? bg = null, int radius = 18, int padH = 18, int padV = 14, int shadow = 8)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Box(bg ?? new Color(Palette.Paper, 0.94f), radius, shadow: shadow, padH: padH, padV: padV));
        return panel;
    }

    public static Button Button(string text, Action onPressed, Audio? audio = null, int minWidth = 240, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(minWidth, 54),
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        if (primary)
        {
            button.AddThemeStyleboxOverride("normal", Box(Palette.Accent, 14, border: Palette.AccentDeep, borderWidth: 2, shadow: 5));
            button.AddThemeStyleboxOverride("hover", Box(Palette.Accent.Lightened(0.12f), 14, border: Palette.AccentDeep, borderWidth: 2, shadow: 7));
            button.AddThemeStyleboxOverride("pressed", Box(Palette.AccentDeep, 14, border: Palette.AccentDeep.Darkened(0.1f), borderWidth: 2, shadow: 1));
            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Colors.White);
        }

        button.Pressed += () =>
        {
            audio?.Play(Sfx.UiClick);
            onPressed();
        };
        AddHoverBounce(button, audio);
        return button;
    }

    public static Button IconButton(string glyph, string tooltip, Action onPressed, Audio? audio = null, int size = 54)
    {
        var button = new Button
        {
            Text = glyph,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(size, size),
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 24);
        button.AddThemeStyleboxOverride("normal", Box(new Color(Palette.Paper, 0.94f), size / 2, shadow: 5));
        button.AddThemeStyleboxOverride("hover", Box(Colors.White, size / 2, border: Palette.Accent, borderWidth: 2, shadow: 7));
        button.AddThemeStyleboxOverride("pressed", Box(Palette.PaperShade, size / 2, border: Palette.AccentDeep, borderWidth: 2, shadow: 2));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(Palette.Paper, 0.5f), size / 2));
        button.Pressed += () =>
        {
            audio?.Play(Sfx.UiClick);
            onPressed();
        };
        AddHoverBounce(button, audio);
        return button;
    }

    public static void AddHoverBounce(Control control, Audio? audio)
    {
        control.Resized += () => control.PivotOffset = control.Size / 2f;
        control.MouseEntered += () =>
        {
            if (control is BaseButton { Disabled: true })
                return;
            audio?.Play(Sfx.UiHover, 1f, -10f);
            control.PivotOffset = control.Size / 2f;
            var tween = control.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(control, "scale", Vector2.One * 1.05f, 0.15);
        };
        control.MouseExited += () =>
        {
            var tween = control.CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(control, "scale", Vector2.One, 0.15);
        };
    }

    public static ProgressBar Bar(Color fill, float height = 10)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.0001,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, height),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        bar.AddThemeStyleboxOverride("fill", Box(fill, (int)(height / 2)));
        bar.AddThemeStyleboxOverride("background", Box(new Color(Palette.Ink, 0.12f), (int)(height / 2)));
        return bar;
    }

    /// <summary>Round coloured badge with a vector icon, used for terrain and rule icons.</summary>
    public static PanelContainer Badge(IconKind icon, Color color, int size = 40)
    {
        var badge = new PanelContainer { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
        badge.AddThemeStyleboxOverride("panel", Box(color, size / 2, border: color.Lightened(0.35f), borderWidth: 2));
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.AddChild(new Icon(icon, Colors.White, size * 0.58f));
        badge.AddChild(center);
        return badge;
    }

    public static Control Spacer(float w = 0, float h = 0) => new Control { CustomMinimumSize = new Vector2(w, h), MouseFilter = Control.MouseFilterEnum.Ignore };

    public static string Number(long value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    private static Texture2D Dot(int size, Color color)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var c = (size - 1) / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2(x - c, y - c).Length();
                var a = Mathf.Clamp((c - d) + 0.5f, 0f, 1f);
                var ring = d > c - 3f ? color.Darkened(0.15f) : color.Lightened(0.15f);
                image.SetPixel(x, y, new Color(d > c - 3f ? Colors.White : ring, a));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D Check(bool on)
    {
        const int size = 28;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var bg = on ? Palette.Accent : Palette.PaperShade.Darkened(0.05f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var inside = RoundRect(x, y, size, 7);
                image.SetPixel(x, y, new Color(bg, inside));
            }
        }

        if (on)
        {
            // Tick mark.
            for (var t = 0f; t <= 1f; t += 0.01f)
            {
                var p = t < 0.4f
                    ? new Vector2(7, 14).Lerp(new Vector2(12, 19), t / 0.4f)
                    : new Vector2(12, 19).Lerp(new Vector2(21, 9), (t - 0.4f) / 0.6f);
                for (var dx = -1; dx <= 1; dx++)
                    for (var dy = -1; dy <= 1; dy++)
                        image.SetPixel(Mathf.Clamp((int)p.X + dx, 0, size - 1), Mathf.Clamp((int)p.Y + dy, 0, size - 1), Colors.White);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static float RoundRect(int x, int y, int size, float r)
    {
        var px = Mathf.Clamp(x + 0.5f, r, size - r);
        var py = Mathf.Clamp(y + 0.5f, r, size - r);
        var d = new Vector2(x + 0.5f - px, y + 0.5f - py).Length();
        return Mathf.Clamp(r - d + 0.5f, 0f, 1f);
    }
}
