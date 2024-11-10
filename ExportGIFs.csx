using System.Text;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UndertaleModLib.Util;
using ImageMagick;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

// Settings
bool doSelected = false;
bool allowPNG = true;
bool sortByUnderscore = true;
int frameMS = 50;

#region FORM

var tooltip = new ToolTip();
Form startupForm = new Form()
{
	Text = "Loy's GIF exporter",
	MaximizeBox = false,
	MinimizeBox = false,
	StartPosition = FormStartPosition.CenterScreen,
	FormBorderStyle = FormBorderStyle.FixedDialog,
	AutoSizeMode = AutoSizeMode.GrowAndShrink,
	AutoSize = true
};

var layoutPanel = new TableLayoutPanel
{
	Dock = DockStyle.Fill,
	RowCount = 4,
	ColumnCount = 2,
	AutoSize = true,
	AutoSizeMode = AutoSizeMode.GrowAndShrink,
	Padding = new Padding(15),
	CellBorderStyle = TableLayoutPanelCellBorderStyle.None
};

var optionsLabel = new Label
{
	Text = "Options",
	AutoSize = true,
	Font = new Font(Label.DefaultFont, FontStyle.Bold)
};
layoutPanel.Controls.Add(optionsLabel, 0, 0);

var boxPNG = new CheckBox()
{
	Text = "Allow PNG",
	Checked = true,
	AutoSize = true
};
layoutPanel.Controls.Add(boxPNG, 0, 1);
tooltip.SetToolTip(boxPNG, "Exports as PNG when the sprite only has one frame.");

var boxUnderscore = new CheckBox()
{
	Text = "Sort From Underscores",
	Checked = false,
	AutoSize = true
};
layoutPanel.Controls.Add(boxUnderscore, 1, 1);
tooltip.SetToolTip(boxUnderscore, "\"spr_player_idle\" would get saved to \"spr\\player\\idle.gif\".");

var frameTimeLabel = new Label
{
	Text = "Frame Time (MS):",
	AutoSize = true
};
layoutPanel.Controls.Add(frameTimeLabel, 0, 2);

var frameTimeInput = new NumericUpDown
{
	Minimum = 0,
	Maximum = 10000,
	Value = 50,
	Increment = 10,
	Width = 100
};
layoutPanel.Controls.Add(frameTimeInput, 1, 2);

var buttonPanel = new TableLayoutPanel
{
	Dock = DockStyle.Bottom,
	RowCount = 1,
	ColumnCount = 2,
	AutoSize = true,
	AutoSizeMode = AutoSizeMode.GrowAndShrink,
	Padding = new Padding(0)
};

var okButton = new Button()
{
	Text = "Start Dump",
	Height = 48,
	DialogResult = DialogResult.OK
};
okButton.Click += (o, s) =>
{
	allowPNG = boxPNG.Checked;
	frameMS = (int)frameTimeInput.Value;
	sortByUnderscore = boxUnderscore.Checked;
};
buttonPanel.Controls.Add(okButton, 0, 0);

var okSelectedButton = new Button()
{
	Text = "Dump Selected",
	Height = 48,
	DialogResult = DialogResult.OK,
	Enabled = Selected is UndertaleSprite
};
okSelectedButton.Click += (sender, e) =>
{
	allowPNG = boxPNG.Checked;
	frameMS = (int)frameTimeInput.Value;
	sortByUnderscore = boxUnderscore.Checked;

	doSelected = true;
};

buttonPanel.Controls.Add(okSelectedButton, 1, 0);

startupForm.Controls.Add(layoutPanel);
startupForm.Controls.Add(buttonPanel);

startupForm.AcceptButton = okButton;

Application.EnableVisualStyles();
DialogResult result = startupForm.ShowDialog();

if (result != DialogResult.OK)
	return;

#endregion
#region PROGRAM

string texFolder = Path.GetDirectoryName(FilePath) + "\\Export_Sprites\\";
int progress = 0;
TextureWorker worker = new TextureWorker();

if (!Directory.Exists(texFolder))
	Directory.CreateDirectory(texFolder);

if (doSelected)
{
	if (Selected is UndertaleSprite)
		DumpSprite((UndertaleSprite)Selected);
}
else
{
	UpdateProgress();
	await Task.Run(() => Parallel.ForEach(Data.Sprites, DumpSprite));
	HideProgressBar();
}
worker.Dispose();

bool openInExplorer;
openInExplorer = ScriptQuestion("Export complete.\n\nOpen the containing folder?");

if (openInExplorer)
	Process.Start("explorer.exe", texFolder); // no need in praying anymore

void UpdateProgress()
{
    UpdateProgressBar(null, "Sprites", progress++, Data.Sprites.Count);
}

void DumpSprite(UndertaleSprite sprite)
{
	if (sprite.Textures == null || sprite.Textures.Count == 0)
		return;

	string filename = texFolder + sprite.Name.Content;

	if (sortByUnderscore)
	{
		filename = texFolder + sprite.Name.Content.Replace("_", "\\");

		string directoryPath = Path.GetDirectoryName(filename);
		if (!Directory.Exists(directoryPath))
			Directory.CreateDirectory(directoryPath);
	}

	if (allowPNG && sprite.Textures.Count == 1 && sprite.Textures[0].Texture != null)
		worker.ExportAsPNG(sprite.Textures[0].Texture, filename + ".png", null, true);
	else
	{
		using var images = new MagickImageCollection();
		for (int i = 0; i < sprite.Textures.Count; i++)
		{
			IMagickImage<byte> image = worker.GetTextureFor(sprite.Textures[i].Texture, sprite.Name.Content + "_" + i, true);
			image.AnimationDelay = frameMS / 10;
			image.BackgroundColor = MagickColors.Transparent;
			image.GifDisposeMethod = GifDisposeMethod.Background;
			images.Add(image);
		}
		images.Coalesce();
		images.OptimizeTransparency();
		images.Write(filename + ".gif");
		images.Dispose();
	}

	if (!doSelected)
		UpdateProgress();
}

#endregion
