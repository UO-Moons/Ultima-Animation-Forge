using System.Collections.Generic;
using System.Linq;
using System.Text;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public static class GumpBuilderExportService
{
    public static string ExportUox3JavaScript(
    IEnumerable<GumpBuilderElement> elements,
    bool useMasterGump,
    int masterGumpId,
    bool noClose,
    bool noDispose,
    bool noMove,
    bool noResize)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("var gump = new Gump;");
        builder.AppendLine();

        if (useMasterGump)
        {
            builder.AppendLine("gump.MasterGump(" + masterGumpId + ");");
        }

        if (noClose)
        {
            builder.AppendLine("gump.NoClose();");
        }

        if (noDispose)
        {
            builder.AppendLine("gump.NoDispose();");
        }

        if (noMove)
        {
            builder.AppendLine("gump.NoMove();");
        }

        if (noResize)
        {
            builder.AppendLine("gump.NoResize();");
        }

        if (useMasterGump || noClose || noDispose || noMove || noResize)
        {
            builder.AppendLine();
        }

        foreach (GumpBuilderElement element in elements)
        {
            switch (element.Type)
            {
                case GumpBuilderElementType.Background:
                    builder.AppendLine(
                        "gump.AddBackground(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.Width + ", " +
                        element.Height + ", " +
                        element.GumpId + ");");
                    break;

                case GumpBuilderElementType.Image:
                    if (element.Hue > 0)
                    {
                        builder.AppendLine(
                            "gump.AddGumpColor(" +
                            element.Y + ", " +
                            element.X + ", " +
                            element.GumpId + ", " +
                            element.Hue + ");");
                    }
                    else
                    {
                        builder.AppendLine(
                            "gump.AddGump(" +
                            element.Y + ", " +
                            element.X + ", " +
                            element.GumpId + ");");
                    }
                    break;

                case GumpBuilderElementType.Button:
                    builder.AppendLine(
                        "gump.AddButton(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.GumpId + ", " +
                        element.PressedGumpId + ", " +
                        element.ButtonId + ", 1, 0);");
                    break;

                case GumpBuilderElementType.ButtonTileArt:
                    builder.AppendLine(
                        "gump.AddButtonTileArt(" +
                        element.Y + ", " +
                        element.X + ", " +
                        element.GumpId + ", " +
                        element.PressedGumpId + ", 1, 1, " +
                        element.ButtonId + ", " +
                        element.TileId + ", " +
                        element.Hue + ", " +
                        element.TileX + ", " +
                        element.TileY + ");");
                    break;

                case GumpBuilderElementType.Checkbox:
                    builder.AppendLine(
                        "gump.AddCheckbox(" +
                        element.Y + ", " +
                        element.X + ", " +
                        element.GumpId + ", " +
                        (element.DefaultStatus ? "1" : "0") + ", " +
                        element.ButtonId + ");");
                    break;

                case GumpBuilderElementType.CheckerTrans:
                    builder.AppendLine(
                        "gump.AddCheckerTrans(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.Width + ", " +
                        element.Height + ");");
                    break;

                case GumpBuilderElementType.Text:
                    builder.AppendLine(
                        "gump.AddText(" +
                        element.Y + ", " +
                        element.X + ", " +
                        element.Hue + ", \"" +
                        EscapeJsString(element.Text) + "\");");
                    break;

                case GumpBuilderElementType.Html:
                    builder.AppendLine(
                        "gump.AddHTMLGump(" +
                        element.Y + ", " +
                        element.X + ", " +
                        element.Width + ", " +
                        element.Height + ", " +
                        ToJsBool(element.HasBackground) + ", " +
                        ToJsBool(element.HasScrollbar) + ", \"" +
                        EscapeJsString(element.Text) + "\");");
                    break;

                case GumpBuilderElementType.XmfHtml:
                    builder.AppendLine(
                        "gump.AddXMFHTMLGump(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.Width + ", " +
                        element.Height + ", " +
                        element.ClilocNumber + ", " +
                        ToJsBool(element.HasBorder) + ", " +
                        ToJsBool(element.HasScrollbar) + ");");
                    break;

                case GumpBuilderElementType.XmfHtmlColor:
                    builder.AppendLine(
                        "gump.AddXMFHTMLGumpColor(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.Width + ", " +
                        element.Height + ", " +
                        element.ClilocNumber + ", " +
                        ToJsBool(element.HasBorder) + ", " +
                        ToJsBool(element.HasScrollbar) + ", " +
                        element.RgbColour + ");");
                    break;

                case GumpBuilderElementType.XmfHtmlTok:
                    builder.AppendLine(BuildXmfHtmlTokLine(element));
                    break;

                case GumpBuilderElementType.TextEntry:
                    if (element.LimitedTextEntry)
                    {
                        builder.AppendLine(
                            "gump.AddTextEntryLimited(" +
                            element.Y + ", " +
                            element.X + ", " +
                            element.Width + ", " +
                            element.Height + ", " +
                            element.Hue + ", " +
                            element.ButtonId + ", " +
                            element.TextId + ", \"" +
                            EscapeJsString(element.Text) + "\", " +
                            element.MaxLength + ");");
                    }
                    else
                    {
                        builder.AppendLine(
                            "gump.AddTextEntry(" +
                            element.Y + ", " +
                            element.X + ", " +
                            element.Width + ", " +
                            element.Height + ", " +
                            element.Hue + ", " +
                            element.ButtonId + ", " +
                            element.TextId + ", \"" +
                            EscapeJsString(element.Text) + "\");");
                    }
                    break;

                case GumpBuilderElementType.CroppedText:
                    builder.AppendLine(
                        "gump.AddCroppedText(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.Hue + ", " +
                        element.Width + ", " +
                        element.Height + ", \"" +
                        EscapeJsString(element.Text) + "\");");
                    break;

                case GumpBuilderElementType.Radio:
                    builder.AppendLine(
                        "gump.AddRadio(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.GumpId + ", " +
                        element.PressedGumpId + ", false, " +
                        element.ButtonId + ");");
                    break;

                case GumpBuilderElementType.TiledImage:
                    builder.AppendLine(
                        "gump.AddTiledGump(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.Width + ", " +
                        element.Height + ", " +
                        element.GumpId + ");");
                    break;

                case GumpBuilderElementType.PicInPic:
                    builder.AppendLine(
                        "gump.AddPicInPic(" +
                        element.X + ", " +
                        element.Y + ", " +
                        element.GumpId + ", " +
                        element.SpriteX + ", " +
                        element.SpriteY + ", " +
                        element.Width + ", " +
                        element.Height + ");");
                    break;

                case GumpBuilderElementType.Item:
                    if (element.Hue > 0)
                    {
                        builder.AppendLine(
                            "gump.AddPictureColor(" +
                            element.Y + ", " +
                            element.X + ", " +
                            element.GumpId + ", " +
                            element.Hue + ");");
                    }
                    else
                    {
                        builder.AppendLine(
                            "gump.AddPicture(" +
                            element.Y + ", " +
                            element.X + ", " +
                            element.GumpId + ");");
                    }
                    break;

                case GumpBuilderElementType.Tooltip:
                    builder.AppendLine(
                        "gump.AddTooltip(\"" +
                        EscapeJsString(element.Text) + "\");");
                    break;

                case GumpBuilderElementType.GroupStart:
                    builder.AppendLine("gump.AddGroup(" + element.GroupNumber + ");");
                    break;

                case GumpBuilderElementType.GroupEnd:
                    builder.AppendLine("gump.EndGroup();");
                    break;

                case GumpBuilderElementType.Page:
                    builder.AppendLine("gump.AddPage(" + element.PageNumber + ");");
                    break;

                case GumpBuilderElementType.ItemProperty:
                    builder.AppendLine("gump.AddItemProperty(" + element.ItemPropertyObject + ");");
                    break;

                case GumpBuilderElementType.ClilocToolTip:
                    builder.AppendLine("gump.AddToolTip(" + element.ClilocNumber + ");");
                    break;

                case GumpBuilderElementType.PageButton:
                    if (element.PressedGumpId > 0 &&
                        element.PressedGumpId != element.GumpId + 1)
                    {
                        builder.AppendLine(
                            "gump.AddPageButton(" +
                            element.Y + ", " +
                            element.X + ", " +
                            element.GumpId + ", " +
                            element.PressedGumpId + ", " +
                            element.PageNumber + ");");
                    }
                    else
                    {
                        builder.AppendLine(
                            "gump.AddPageButton(" +
                            element.Y + ", " +
                            element.X + ", " +
                            element.GumpId + ", " +
                            element.PageNumber + ");");
                    }
                    break;
            }
        }

        builder.AppendLine();
        builder.AppendLine("gump.Send(socket);");
        builder.AppendLine("gump.Free();");

        builder.AppendLine();
        builder.AppendLine(BuildOnGumpPressSkeleton(elements));

        return builder.ToString();
    }

    private static string EscapeJsString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", string.Empty)
            .Replace("\n", "\\n");
    }

    private static string ToJsBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string BuildXmfHtmlTokLine(GumpBuilderElement element)
    {
        List<string> args = new List<string>
    {
        element.X.ToString(),
        element.Y.ToString(),
        element.Width.ToString(),
        element.Height.ToString(),
        ToJsBool(element.HasBorder),
        ToJsBool(element.HasScrollbar),
        element.RgbColour.ToString(),
        element.ClilocNumber.ToString()
    };

        AddOptionalClilocArg(args, element.ClilocArg1);
        AddOptionalClilocArg(args, element.ClilocArg2);
        AddOptionalClilocArg(args, element.ClilocArg3);

        return "gump.AddXMFHTMLTok(" + string.Join(", ", args) + ");";
    }

    private static void AddOptionalClilocArg(List<string> args, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (int.TryParse(value, out _))
        {
            args.Add(value);
        }
        else
        {
            args.Add("\"" + EscapeJsString(value) + "\"");
        }
    }

    private static string BuildOnGumpPressSkeleton(IEnumerable<GumpBuilderElement> elements)
    {
        List<GumpBuilderElement> normalButtons = elements
            .Where(x =>
                (x.Type == GumpBuilderElementType.Button ||
                 x.Type == GumpBuilderElementType.ButtonTileArt) &&
                x.ButtonId > 0)
            .OrderBy(x => x.ButtonId)
            .ToList();

        List<GumpBuilderElement> radiosAndCheckboxes = elements
            .Where(x =>
                (x.Type == GumpBuilderElementType.Radio ||
                 x.Type == GumpBuilderElementType.Checkbox) &&
                x.ButtonId >= 0)
            .OrderBy(x => x.ButtonId)
            .ToList();

        List<GumpBuilderElement> textEntries = elements
            .Where(x => x.Type == GumpBuilderElementType.TextEntry)
            .ToList();

        StringBuilder builder = new StringBuilder();

        builder.AppendLine("function onGumpPress( pSock, pButton, gumpData )");
        builder.AppendLine("{");
        builder.AppendLine("\tvar pUser = pSock.currentChar;");
        builder.AppendLine();
        builder.AppendLine("\tswitch( pButton )");
        builder.AppendLine("\t{");
        builder.AppendLine("\t\tcase 0:");
        builder.AppendLine("\t\t\t// Gump closed/cancelled.");
        builder.AppendLine("\t\t\tbreak;");

        foreach (GumpBuilderElement button in normalButtons)
        {
            builder.AppendLine();
            builder.AppendLine("\t\tcase " + button.ButtonId + ":");
            builder.AppendLine("\t\t\t// " + EscapeJsComment(button.Name));

            for (int i = 0; i < textEntries.Count; i++)
            {
                builder.AppendLine("\t\t\tvar textEntry" + i + " = gumpData.getEdit(" + i + ");");
            }

            if (radiosAndCheckboxes.Count > 0)
            {
                builder.AppendLine("\t\t\tvar selectedButton = gumpData.getButton(0);");
                builder.AppendLine("\t\t\tswitch( selectedButton )");
                builder.AppendLine("\t\t\t{");

                foreach (GumpBuilderElement choice in radiosAndCheckboxes)
                {
                    builder.AppendLine("\t\t\t\tcase " + choice.ButtonId + ":");
                    builder.AppendLine("\t\t\t\t\t// " + EscapeJsComment(choice.Name));
                    builder.AppendLine("\t\t\t\t\tbreak;");
                }

                builder.AppendLine("\t\t\t}");
            }

            builder.AppendLine("\t\t\tbreak;");
        }

        builder.AppendLine("\t}");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string EscapeJsComment(string value)
    {
        return (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("*/", "* /");
    }

    public static string ExportCSharp(IEnumerable<GumpBuilderElement> elements)
    {
        List<GumpBuilderElement> ordered = elements
            .OrderBy(x => x.Z)
            .ToList();

        StringBuilder layout = new StringBuilder();
        StringBuilder controls = new StringBuilder();

        foreach (GumpBuilderElement element in ordered)
        {
            string? line = BuildCSharpElementLine(element);

            if (!string.IsNullOrWhiteSpace(line))
            {
                layout.AppendLine("            " + line);
            }
        }

        BuildCSharpEnums(ordered, controls);

        return
    @"using System;

using Server;
using Server.Commands;
using Server.Gumps;
using Server.Network;

namespace Server.Gumps
{
    public class CustomGump : Gump
    {
        public static void Configure()
        {
            CommandSystem.Register(""CustomGump"", AccessLevel.Administrator, e => DisplayTo(e.Mobile));
        }

        public static CustomGump DisplayTo(Mobile user)
        {
            if (user == null || user.Deleted || !user.Player || user.NetState == null)
            {
                return null;
            }

            user.CloseGump(typeof(CustomGump));

            CustomGump gump = new CustomGump(user);
            user.SendGump(gump);

            return gump;
        }

        public Mobile User { get; }

        private CustomGump(Mobile user)
            : base(0, 0)
        {
            User = user;

            Dragable = true;
            Closable = true;
            Resizable = false;
            Disposable = false;

            AddPage(0);
" + layout.ToString().TrimEnd() + @"
        }
" + controls.ToString() + @"
        public override void OnResponse(NetState sender, RelayInfo info)
        {
            switch (info.ButtonID)
            {
                case 0:
                    break;

                default:
                    break;
            }
        }

        public override void OnServerClose(NetState owner)
        {
        }
    }
}";
    }

    private static string? BuildCSharpElementLine(GumpBuilderElement element)
    {
        switch (element.Type)
        {
            case GumpBuilderElementType.Background:
                return "AddBackground(" + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ", " + element.GumpId + ");";

            case GumpBuilderElementType.Image:
                return element.Hue > 0
                    ? "AddImage(" + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.Hue + ");"
                    : "AddImage(" + element.X + ", " + element.Y + ", " + element.GumpId + ");";

            case GumpBuilderElementType.Item:
                return element.Hue > 0
                    ? "AddItem(" + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.Hue + ");"
                    : "AddItem(" + element.X + ", " + element.Y + ", " + element.GumpId + ");";

            case GumpBuilderElementType.Button:
                return "AddButton(" + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.PressedGumpId + ", " + element.ButtonId + ", GumpButtonType.Reply, 0);";

            case GumpBuilderElementType.PageButton:
                return "AddButton(" + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.PressedGumpId + ", 0, GumpButtonType.Page, " + element.PageNumber + ");";

            case GumpBuilderElementType.Text:
                return "AddLabel(" + element.X + ", " + element.Y + ", " + element.Hue + ", \"" + EscapeCSharpString(element.Text) + "\");";

            case GumpBuilderElementType.CroppedText:
                return "AddLabelCropped(" + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ", " + element.Hue + ", \"" + EscapeCSharpString(element.Text) + "\");";

            case GumpBuilderElementType.Html:
                return "AddHtml(" + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ", \"" + EscapeCSharpString(element.Text) + "\", " + ToCSharpBool(element.HasBackground) + ", " + ToCSharpBool(element.HasScrollbar) + ");";

            case GumpBuilderElementType.TextEntry:
                return "AddTextEntry(" + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ", " + element.Hue + ", " + element.TextId + ", \"" + EscapeCSharpString(element.Text) + "\");";

            case GumpBuilderElementType.Checkbox:
                return "AddCheck(" + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.PressedGumpId + ", " + ToCSharpBool(element.DefaultStatus) + ", " + element.ButtonId + ");";

            case GumpBuilderElementType.Radio:
                return "AddRadio(" + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.PressedGumpId + ", " + ToCSharpBool(element.DefaultStatus) + ", " + element.ButtonId + ");";

            case GumpBuilderElementType.TiledImage:
                return "AddImageTiled(" + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ", " + element.GumpId + ");";

            case GumpBuilderElementType.CheckerTrans:
                return "AddAlphaRegion(" + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ");";

            case GumpBuilderElementType.Page:
                return "AddPage(" + element.PageNumber + ");";

            case GumpBuilderElementType.ClilocToolTip:
                return "AddTooltip(" + element.ClilocNumber + ");";

            default:
                return "// Unsupported C# export: " + element.Type;
        }
    }

    private static void BuildCSharpEnums(List<GumpBuilderElement> elements, StringBuilder controls)
    {
        List<GumpBuilderElement> buttons = elements
            .Where(x => x.Type == GumpBuilderElementType.Button || x.Type == GumpBuilderElementType.ButtonTileArt)
            .Where(x => x.ButtonId > 0)
            .OrderBy(x => x.ButtonId)
            .ToList();

        if (buttons.Count > 0)
        {
            controls.AppendLine();
            controls.AppendLine("        public enum Buttons");
            controls.AppendLine("        {");

            foreach (GumpBuilderElement button in buttons)
            {
                controls.AppendLine("            " + MakeCSharpName(button.Name, "Button") + " = " + button.ButtonId + ",");
            }

            controls.AppendLine("        }");
        }

        List<GumpBuilderElement> switches = elements
            .Where(x => x.Type == GumpBuilderElementType.Checkbox || x.Type == GumpBuilderElementType.Radio)
            .OrderBy(x => x.ButtonId)
            .ToList();

        if (switches.Count > 0)
        {
            controls.AppendLine();
            controls.AppendLine("        public enum Switches");
            controls.AppendLine("        {");

            foreach (GumpBuilderElement sw in switches)
            {
                controls.AppendLine("            " + MakeCSharpName(sw.Name, "Switch") + " = " + sw.ButtonId + ",");
            }

            controls.AppendLine("        }");
        }

        List<GumpBuilderElement> inputs = elements
            .Where(x => x.Type == GumpBuilderElementType.TextEntry)
            .OrderBy(x => x.TextId)
            .ToList();

        if (inputs.Count > 0)
        {
            controls.AppendLine();
            controls.AppendLine("        public enum Inputs");
            controls.AppendLine("        {");

            foreach (GumpBuilderElement input in inputs)
            {
                controls.AppendLine("            " + MakeCSharpName(input.Name, "Input") + " = " + input.TextId + ",");
            }

            controls.AppendLine("        }");
        }
    }

    private static string MakeCSharpName(string value, string fallback)
    {
        string text = string.IsNullOrWhiteSpace(value) ? fallback : value;

        StringBuilder builder = new StringBuilder();

        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                builder.Append(c);
            }
        }

        if (builder.Length == 0)
        {
            builder.Append(fallback);
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, fallback);
        }

        return builder.ToString();
    }

    private static string ToCSharpBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string EscapeCSharpString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    public static string ExportSphere(
    IEnumerable<GumpBuilderElement> elements,
    string gumpName,
    bool revision,
    bool noClose,
    bool noDispose,
    bool noMove)
    {
        List<GumpBuilderElement> ordered = elements
            .OrderBy(x => x.Z)
            .ToList();

        StringBuilder builder = new StringBuilder();
        List<(int id, string text)> buttonBlocks = new();
        List<(int id, string text)> textBlocks = new();

        int textId = 0;
        int pageButtonAutoId = 0;
        int checkboxAutoId = 0;
        int currentGroup = -1;

        string safeName = string.IsNullOrWhiteSpace(gumpName) ? "d_default" : gumpName.Trim();

        builder.AppendLine("[DIALOG " + safeName + "]");
        builder.AppendLine(revision ? "0,0" : "SetLocation=0,0");

        if (noClose)
        {
            builder.AppendLine(revision ? "NOCLOSE" : "NoClose");
        }

        if (noMove)
        {
            builder.AppendLine(revision ? "NOMOVE" : "NoMove");
        }

        if (noDispose)
        {
            builder.AppendLine(revision ? "NODISPOSE" : "NoDispose");
        }

        builder.AppendLine(revision ? "page 0" : "Page(0)");

        foreach (GumpBuilderElement element in ordered)
        {
            string? line = BuildSphereElementLine(
                element,
                revision,
                ref textId,
                ref pageButtonAutoId,
                ref checkboxAutoId,
                ref currentGroup,
                textBlocks,
                buttonBlocks);

            if (!string.IsNullOrWhiteSpace(line))
            {
                builder.AppendLine(line);
            }
        }

        builder.AppendLine();

        if (revision && textBlocks.Count > 0)
        {
            builder.AppendLine("[DIALOG " + safeName + " text]");

            foreach ((int id, string text) in textBlocks.OrderBy(x => x.id))
            {
                builder.AppendLine(text);
            }

            builder.AppendLine();
        }

        builder.AppendLine("[DIALOG " + safeName + " button]");

        foreach ((int id, string text) in buttonBlocks.OrderBy(x => x.id))
        {
            builder.AppendLine("ON=" + id);
            builder.AppendLine(text);
            builder.AppendLine();
        }

        builder.AppendLine("[EOF]");

        return builder.ToString();
    }

    private static string? BuildSphereElementLine(
    GumpBuilderElement element,
    bool revision,
    ref int textId,
    ref int pageButtonAutoId,
    ref int checkboxAutoId,
    ref int currentGroup,
    List<(int id, string text)> textBlocks,
    List<(int id, string text)> buttonBlocks)
    {
        switch (element.Type)
        {
            case GumpBuilderElementType.Page:
                return revision
                    ? "page " + element.PageNumber
                    : "Page(" + element.PageNumber + ")";

            case GumpBuilderElementType.Background:
                return revision
                    ? "resizepic " + element.X + " " + element.Y + " " + element.GumpId + " " + element.Width + " " + element.Height
                    : "ResizePic(" + element.X + "," + element.Y + "," + element.GumpId + "," + element.Width + "," + element.Height + ")";

            case GumpBuilderElementType.CheckerTrans:
                return revision
                    ? "checkertrans " + element.X + " " + element.Y + " " + element.Width + " " + element.Height
                    : "CheckerTrans(" + element.X + "," + element.Y + "," + element.Width + "," + element.Height + ")";

            case GumpBuilderElementType.Image:
                return BuildSphereGumpPic(element, revision);

            case GumpBuilderElementType.Item:
                return BuildSphereTilePic(element, revision);

            case GumpBuilderElementType.TiledImage:
                return revision
                    ? "gumppictiled " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " + element.GumpId
                    : "GumpPicTiled(" + element.X + "," + element.Y + "," + element.Width + "," + element.Height + "," + element.GumpId + ")";

            case GumpBuilderElementType.Button:
            case GumpBuilderElementType.ButtonTileArt:
                buttonBlocks.Add((element.ButtonId, "// " + element.Name));
                return revision
                    ? "button " + element.X + " " + element.Y + " " + element.GumpId + " " + element.PressedGumpId + " 1 0 " + element.ButtonId
                    : "Button(" + element.X + "," + element.Y + "," + element.GumpId + "," + element.PressedGumpId + ",1,0," + element.ButtonId + ")";

            case GumpBuilderElementType.PageButton:
                return revision
                    ? "button " + element.X + " " + element.Y + " " + element.GumpId + " " + element.PressedGumpId + " 1 " + element.PageNumber + " " + pageButtonAutoId++
                    : "Button(" + element.X + "," + element.Y + "," + element.GumpId + "," + element.PressedGumpId + ",1," + element.PageNumber + "," + pageButtonAutoId++ + ")";

            case GumpBuilderElementType.Text:
                if (revision)
                {
                    int id = textId++;
                    textBlocks.Add((id, string.IsNullOrEmpty(element.Text) ? "Text id." + id : element.Text));
                    return "text " + element.X + " " + element.Y + " " + element.Hue + " " + id;
                }

                return "TextA(" + element.X + "," + element.Y + "," + element.Hue + ",\"" + EscapeSphereString(element.Text) + "\")";

            case GumpBuilderElementType.Html:
                if (revision)
                {
                    int id = textId++;
                    textBlocks.Add((id, string.IsNullOrEmpty(element.Text) ? "HtmlGump id." + id : element.Text));
                    return "htmlgump " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " + id + " " +
                           (element.HasBackground ? "1" : "0") + " " +
                           (element.HasScrollbar ? "1" : "0");
                }

                return "HtmlGumpA(" + element.X + "," + element.Y + "," + element.Width + "," + element.Height + ",\"" +
                       EscapeSphereString(element.Text) + "\"," +
                       (element.HasBackground ? "1" : "0") + "," +
                       (element.HasScrollbar ? "1" : "0") + ")";

            case GumpBuilderElementType.XmfHtml:
                return revision
                    ? "xmfhtmlgump " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " + element.ClilocNumber + " " + (element.HasBorder ? "1" : "0") + " " + (element.HasScrollbar ? "1" : "0")
                    : "XmfHtmlGump(" + element.X + "," + element.Y + "," + element.Width + "," + element.Height + "," + element.ClilocNumber + "," + (element.HasBorder ? "1" : "0") + "," + (element.HasScrollbar ? "1" : "0") + ")";

            case GumpBuilderElementType.TextEntry:
                if (revision)
                {
                    int id = textId++;
                    textBlocks.Add((id, string.IsNullOrEmpty(element.Text) ? "Textentry id." + element.TextId : element.Text));
                    return "textentry " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " + element.Hue + " " + element.TextId + " " + id;
                }

                return "TextEntryA(" + element.X + "," + element.Y + "," + element.Width + "," + element.Height + "," + element.Hue + "," + element.TextId + ",\"" + EscapeSphereString(element.Text) + "\")";

            case GumpBuilderElementType.Checkbox:
                return revision
                    ? "checkbox " + element.X + " " + element.Y + " " + element.PressedGumpId + " " + element.GumpId + " " + (element.DefaultStatus ? "1" : "0") + " " + checkboxAutoId++
                    : "CheckBox(" + element.X + "," + element.Y + "," + element.PressedGumpId + "," + element.GumpId + "," + (element.DefaultStatus ? "1" : "0") + "," + checkboxAutoId++ + ")";

            case GumpBuilderElementType.GroupStart:
                currentGroup = element.GroupNumber;
                return revision
                    ? "Group " + element.GroupNumber
                    : "Group(" + element.GroupNumber + ")";

            case GumpBuilderElementType.Radio:
                StringBuilder radio = new StringBuilder();

                if (element.GroupNumber != currentGroup)
                {
                    radio.AppendLine(revision
                        ? "Group " + element.GroupNumber
                        : "Group(" + element.GroupNumber + ")");

                    currentGroup = element.GroupNumber;
                }

                radio.Append(revision
                    ? "radio " + element.X + " " + element.Y + " " + element.PressedGumpId + " " + element.GumpId + " " + (element.DefaultStatus ? "1" : "0") + " " + element.ButtonId
                    : "Radio(" + element.X + "," + element.Y + "," + element.PressedGumpId + "," + element.GumpId + "," + (element.DefaultStatus ? "1" : "0") + "," + element.ButtonId + ")");

                return radio.ToString();

            default:
                return "// Unsupported Sphere export: " + element.Type;
        }
    }

    private static string BuildSphereGumpPic(GumpBuilderElement element, bool revision)
    {
        bool hasHue = element.Hue > 0;

        if (revision)
        {
            return hasHue
                ? "gumppic " + element.X + " " + element.Y + " " + element.GumpId + " " + element.Hue
                : "gumppic " + element.X + " " + element.Y + " " + element.GumpId;
        }

        return hasHue
            ? "GumpPic(" + element.X + "," + element.Y + "," + element.GumpId + "," + element.Hue + ")"
            : "GumpPic(" + element.X + "," + element.Y + "," + element.GumpId + ")";
    }

    private static string BuildSphereTilePic(GumpBuilderElement element, bool revision)
    {
        bool hasHue = element.Hue > 0;

        if (revision)
        {
            return hasHue
                ? "tilepichue " + element.X + " " + element.Y + " " + element.GumpId + " " + element.Hue
                : "tilepic " + element.X + " " + element.Y + " " + element.GumpId;
        }

        return hasHue
            ? "TilePicHue(" + element.X + "," + element.Y + "," + element.GumpId + "," + element.Hue + ")"
            : "TilePic(" + element.X + "," + element.Y + "," + element.GumpId + ")";
    }

    private static string EscapeSphereString(string value)
    {
        return (value ?? " ")
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    public static string ExportPol(
    IEnumerable<GumpBuilderElement> elements,
    string gumpName,
    bool useDistro,
    bool noClose,
    bool noDispose,
    bool noMove)
    {
        return useDistro
            ? ExportPolDistro(elements, gumpName, noClose, noDispose, noMove)
            : ExportPolBare(elements, gumpName, noClose, noDispose, noMove);
    }

    private static string ExportPolBare(
    IEnumerable<GumpBuilderElement> elements,
    string gumpName,
    bool noClose,
    bool noDispose,
    bool noMove)
    {
        List<GumpBuilderElement> ordered = elements
            .OrderBy(x => x.Z)
            .ToList();

        List<string> gumpLines = new List<string>();
        List<string> dataLines = new List<string>();

        int textId = 0;
        int currentGroup = -1;

        if (noMove)
        {
            gumpLines.Add("NoMove");
        }

        if (noClose)
        {
            gumpLines.Add("NoClose");
        }

        if (noDispose)
        {
            gumpLines.Add("NoDispose");
        }

        if (!ordered.Any(x => x.Type == GumpBuilderElementType.Page))
        {
            gumpLines.Add("page 0");
        }

        foreach (GumpBuilderElement element in ordered)
        {
            string? line = BuildPolBareElementLine(element, dataLines, ref textId, ref currentGroup);

            if (!string.IsNullOrWhiteSpace(line))
            {
                foreach (string splitLine in line.Replace("\r", string.Empty).Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(splitLine))
                    {
                        gumpLines.Add(splitLine);
                    }
                }
            }
        }

        string safeName = SanitizePolName(gumpName);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine("// Exported from Ultima Animation Forge Gump Builder.");
        builder.AppendLine();
        builder.AppendLine("use uo;");
        builder.AppendLine("use os;");
        builder.AppendLine();
        builder.AppendLine("program gump_" + safeName + "(who)");
        builder.AppendLine();
        builder.AppendLine("\tvar gump := array {");

        for (int i = 0; i < gumpLines.Count; i++)
        {
            builder.Append("\t\t\"" + EscapePolArrayString(gumpLines[i]) + "\"");

            if (i < gumpLines.Count - 1)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        builder.AppendLine("\t};");
        builder.AppendLine("\tvar data := array {");

        for (int i = 0; i < dataLines.Count; i++)
        {
            builder.Append("\t\t\"" + EscapePolArrayString(dataLines[i]) + "\"");

            if (i < dataLines.Count - 1)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        builder.AppendLine("\t};");
        builder.AppendLine();
        builder.AppendLine("\tSendDialogGump(who, gump, data);");
        builder.AppendLine();
        builder.AppendLine("endprogram");

        return builder.ToString();
    }

    private static string ExportPolDistro(
    IEnumerable<GumpBuilderElement> elements,
    string gumpName,
    bool noClose,
    bool noDispose,
    bool noMove)
    {
        List<GumpBuilderElement> ordered = elements
            .OrderBy(x => x.Z)
            .ToList();

        string safeName = SanitizePolName(gumpName);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine("// Exported from Ultima Animation Forge Gump Builder.");
        builder.AppendLine("// POL gump package / distro mode.");
        builder.AppendLine();
        builder.AppendLine("use uo;");
        builder.AppendLine("use os;");
        builder.AppendLine();
        builder.AppendLine("include \":gumps:gumps\";");
        builder.AppendLine();
        builder.AppendLine("program gump_" + safeName + "(who)");
        builder.AppendLine();
        builder.AppendLine("\tvar " + safeName + " := GFCreateGump();");

        if (noMove)
        {
            builder.AppendLine("\tGFMovable(" + safeName + ", 0);");
        }

        if (noClose)
        {
            builder.AppendLine("\tGFClosable(" + safeName + ", 0);");
        }

        if (noDispose)
        {
            builder.AppendLine("\tGFDisposable(" + safeName + ", 0);");
        }

        if (!ordered.Any(x => x.Type == GumpBuilderElementType.Page))
        {
            builder.AppendLine("\tGFPage(" + safeName + ", 0);");
        }

        int currentGroup = -1;

        foreach (GumpBuilderElement element in ordered)
        {
            string? line = BuildPolDistroElementLine(element, safeName, ref currentGroup);

            if (!string.IsNullOrWhiteSpace(line))
            {
                foreach (string splitLine in line.Replace("\r", string.Empty).Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(splitLine))
                    {
                        builder.AppendLine("\t" + splitLine);
                    }
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("\tGFSendGump(who, " + safeName + ");");
        builder.AppendLine();
        builder.AppendLine("endprogram");

        return builder.ToString();
    }

    private static string? BuildPolBareElementLine(
    GumpBuilderElement element,
    List<string> dataLines,
    ref int textId,
    ref int currentGroup)
    {
        switch (element.Type)
        {
            case GumpBuilderElementType.Page:
                return "page " + element.PageNumber;

            case GumpBuilderElementType.Background:
                return "resizepic " + element.X + " " + element.Y + " " + element.GumpId + " " + element.Width + " " + element.Height;

            case GumpBuilderElementType.CheckerTrans:
                return "checkertrans " + element.X + " " + element.Y + " " + element.Width + " " + element.Height;

            case GumpBuilderElementType.Image:
                return element.Hue > 0
                    ? "gumppic " + element.X + " " + element.Y + " " + element.GumpId + " " + element.Hue
                    : "gumppic " + element.X + " " + element.Y + " " + element.GumpId;

            case GumpBuilderElementType.Item:
                return element.Hue > 0
                    ? "tilepichue " + element.X + " " + element.Y + " " + element.GumpId + " " + element.Hue
                    : "tilepic " + element.X + " " + element.Y + " " + element.GumpId;

            case GumpBuilderElementType.TiledImage:
                return "gumppictiled " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " + element.GumpId;

            case GumpBuilderElementType.Button:
            case GumpBuilderElementType.ButtonTileArt:
                return "button " + element.X + " " + element.Y + " " + element.GumpId + " " + element.PressedGumpId + " 0 " + element.ButtonId + " 0";

            case GumpBuilderElementType.PageButton:
                return "button " + element.X + " " + element.Y + " " + element.GumpId + " " + element.PressedGumpId + " 1 0 " + element.PageNumber;

            case GumpBuilderElementType.Checkbox:
                return "checkbox " + element.X + " " + element.Y + " " + element.GumpId + " " + element.PressedGumpId + " " + ToPolBool(element.DefaultStatus) + " " + element.ButtonId;

            case GumpBuilderElementType.GroupStart:
                currentGroup = element.GroupNumber;
                return "group " + element.GroupNumber;

            case GumpBuilderElementType.Radio:
                StringBuilder radio = new StringBuilder();

                if (element.GroupNumber != currentGroup)
                {
                    radio.AppendLine("group " + element.GroupNumber);
                    currentGroup = element.GroupNumber;
                }

                radio.Append("radio " + element.X + " " + element.Y + " " + element.GumpId + " " + element.PressedGumpId + " " + ToPolBool(element.DefaultStatus) + " " + element.ButtonId);
                return radio.ToString();

            case GumpBuilderElementType.Text:
                int labelTextId = dataLines.Count;
                dataLines.Add(string.IsNullOrEmpty(element.Text) ? "Text id." + labelTextId : element.Text);
                return "text " + element.X + " " + element.Y + " " + element.Hue + " " + labelTextId;

            case GumpBuilderElementType.Html:
                int htmlTextId = dataLines.Count;
                dataLines.Add(string.IsNullOrEmpty(element.Text) ? "HtmlGump id." + htmlTextId : element.Text);
                return "htmlgump " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " + htmlTextId + " " +
                       ToPolBool(element.HasBackground) + " " + ToPolBool(element.HasScrollbar);

            case GumpBuilderElementType.XmfHtml:
                return "xmfhtmlgump " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " +
                       element.ClilocNumber + " " + ToPolBool(element.HasBorder) + " " + ToPolBool(element.HasScrollbar);

            case GumpBuilderElementType.TextEntry:
                int entryTextId = dataLines.Count;
                dataLines.Add(string.IsNullOrEmpty(element.Text) ? "TextEntry id." + entryTextId : element.Text);
                return "textentry " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " +
                       element.Hue + " " + element.TextId + " " + entryTextId;

            default:
                return "// Unsupported POL bare export: " + element.Type;
        }
    }

    private static string? BuildPolDistroElementLine(
    GumpBuilderElement element,
    string gumpName,
    ref int currentGroup)
    {
        switch (element.Type)
        {
            case GumpBuilderElementType.Page:
                return "GFPage(" + gumpName + ", " + element.PageNumber + ");";

            case GumpBuilderElementType.Background:
                return "GFResizePic(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.Width + ", " + element.Height + ");";

            case GumpBuilderElementType.CheckerTrans:
                return "GFAddAlphaRegion(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ");";

            case GumpBuilderElementType.Image:
                return "GFGumpPic(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.Hue + ");";

            case GumpBuilderElementType.Item:
                return "GFTilePic(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.Hue + ");";

            case GumpBuilderElementType.TiledImage:
                return "// Gump package does not support GumpPicTiled\n// gumppictiled " + element.X + " " + element.Y + " " + element.Width + " " + element.Height + " " + element.GumpId;

            case GumpBuilderElementType.Button:
            case GumpBuilderElementType.ButtonTileArt:
                return "GFAddButton(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.PressedGumpId + ", GF_CLOSE_BTN, " + element.ButtonId + ");";

            case GumpBuilderElementType.PageButton:
                return "GFAddButton(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.PressedGumpId + ", GF_PAGE_BTN, " + element.PageNumber + ");";

            case GumpBuilderElementType.Checkbox:
                return "GFCheckBox(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.PressedGumpId + ", " + ToPolBool(element.DefaultStatus) + ", " + element.ButtonId + ");";

            case GumpBuilderElementType.GroupStart:
                currentGroup = element.GroupNumber;
                return "GFSetRadioGroup(" + gumpName + ", " + element.GroupNumber + ");";

            case GumpBuilderElementType.Radio:
                StringBuilder radio = new StringBuilder();

                if (element.GroupNumber != currentGroup)
                {
                    radio.AppendLine("GFSetRadioGroup(" + gumpName + ", " + element.GroupNumber + ");");
                    currentGroup = element.GroupNumber;
                }

                radio.Append("GFRadioButton(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.GumpId + ", " + element.PressedGumpId + ", " + ToPolBool(element.DefaultStatus) + ", " + element.ButtonId + ");");
                return radio.ToString();

            case GumpBuilderElementType.Text:
                return "GFTextLine(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.Hue + ", \"" + EscapePolCodeString(element.Text) + "\");";

            case GumpBuilderElementType.Html:
                return "GFHTMLArea(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ", \"" +
                       EscapePolCodeString(element.Text) + "\", " + ToPolBool(element.HasBackground) + ", " + ToPolBool(element.HasScrollbar) + ");";

            case GumpBuilderElementType.XmfHtml:
                return "GFAddHTMLLocalized(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ", " +
                       element.ClilocNumber + ", " + ToPolBool(element.HasBorder) + ", " + ToPolBool(element.HasScrollbar) + ");";

            case GumpBuilderElementType.TextEntry:
                return "GFTextEntry(" + gumpName + ", " + element.X + ", " + element.Y + ", " + element.Width + ", " + element.Height + ", " +
                       element.Hue + ", \"" + EscapePolCodeString(element.Text) + "\", " + element.TextId + ");";

            default:
                return "// Unsupported POL distro export: " + element.Type;
        }
    }

    private static string ToPolBool(bool value)
    {
        return value ? "1" : "0";
    }

    private static string EscapePolArrayString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string EscapePolCodeString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string SanitizePolName(string value)
    {
        string source = string.IsNullOrWhiteSpace(value) ? "gump" : value.Trim();

        StringBuilder builder = new StringBuilder();

        foreach (char c in source)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                builder.Append(c);
            }
        }

        if (builder.Length == 0)
        {
            builder.Append("gump");
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, "gump_");
        }

        return builder.ToString();
    }
}