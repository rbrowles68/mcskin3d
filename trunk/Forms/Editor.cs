﻿//
//    MCSkin3D, a 3d skin management studio for Minecraft
//    Copyright (C) 2011-2012 Altered Softworks & MCSkin3D Team
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Devcorp.Controls.Design;
using MB.Controls;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Security.Cryptography;
using Paril.Settings;
using Paril.Settings.Serializers;
using Paril.Components;
using Paril.Components.Shortcuts;
using Paril.Components.Update;
using System.Runtime.InteropServices;
using System.Collections;
using Paril.Compatibility;
using System.Drawing.Drawing2D;
using Paril.OpenGL;
using OpenTK.Graphics;
using System.Windows.Forms.VisualStyles;
using DragDropLib;
using Paril.Extensions;
using MCSkin3D.Language;
using System.Globalization;
using MCSkin3D.Forms;
using Version = Paril.Components.Update.Version;
using Paril.Drawing;
using Paril.Controls;
using MCSkin3D.lemon42;
using System.Reflection;

namespace MCSkin3D
{
	public partial class Editor : Form
	{
		// ===============================================
		// Private/Static variables
		// ===============================================
		#region Variables
		Updater _updater;

		ColorSliderRenderer redRenderer, greenRenderer, blueRenderer;
		AlphaSliderRenderer alphaRenderer;
		HueSliderRenderer hueRenderer;
		SaturationSliderRenderer saturationRenderer;
		ValueSliderRenderer valueRenderer;

		static ShortcutEditor _shortcutEditor = new ShortcutEditor();
		Texture _grassTop, _alphaTex, _previewPaint;
		Dictionary<Size, Texture> _charPaintSizes = new Dictionary<Size, Texture>();

		float _animationTime = 0;
		float _2dCamOffsetX = 0;
		float _2dCamOffsetY = 0;
		float _2dZoom = 8;
		float _3dZoom = -80;
		float _3dRotationX = 180, _3dRotationY = 0;
		Vector3 _3dOffset = Vector3.Zero;
		bool _mouseIsDown = false;
		Point _mousePoint;
		UndoBuffer _currentUndoBuffer = null;
		Skin _lastSkin = null;
		bool _skipListbox = false;
		internal PleaseWait _pleaseWaitForm;
		
		ColorManager
					_primaryColor = ColorManager.FromRGBA(255, 255, 255, 255),
					_secondaryColor = ColorManager.FromRGBA(0, 0, 0, 255);
		
		bool _skipColors = false;
		ViewMode _currentViewMode = ViewMode.Perspective;
		Renderer _renderer;
		List<BackgroundImage> _backgrounds = new List<BackgroundImage>();
		int _selectedBackground = 0;
		GLControl rendererControl;
		Texture _toolboxUpNormal, _toolboxUpHover, _toolboxDownNormal, _toolboxDownHover;
		Texture _font;

		List<ToolIndex> _tools = new List<ToolIndex>();
		ToolIndex _selectedTool;
		UndoRedoPanel _undoListBox, _redoListBox;
		#endregion

		public ToolIndex SelectedTool { get { return _selectedTool; } }

		public DodgeBurnOptions DodgeBurnOptions { get; private set; }
		public DarkenLightenOptions DarkenLightenOptions { get; private set; }
		public PencilOptions PencilOptions { get; private set; }
		public FloodFillOptions FloodFillOptions { get; private set; }
		public NoiseOptions NoiseOptions { get; private set; }
		public EraserOptions EraserOptions { get; private set; }
		public StampOptions StampOptions { get; private set; }

		public static Editor MainForm { get; private set; }

		public class ModelToolStripMenuItem : ToolStripMenuItem
		{
			public Model Model;

			public ModelToolStripMenuItem(Model model) :
				base(model.Name)
			{
				Name = model.Name;
				Model = model;
				Model.DropDownItem = this;
			}

			protected override void OnClick(EventArgs e)
			{
				MainForm.SetModel(Model);
			}
		}

		// ===============================================
		// Constructor
		// ===============================================
		#region Constructor
		public Editor()
		{
			Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			MainForm = this;
			InitializeComponent();

			bool settingsLoaded = GlobalSettings.Load();

			Icon = Properties.Resources.Icon_new;

			LanguageLoader.LoadLanguages("Languages");

			DodgeBurnOptions = new DodgeBurnOptions();
			DarkenLightenOptions = new DarkenLightenOptions();
			PencilOptions = new PencilOptions();
			FloodFillOptions = new FloodFillOptions();
			NoiseOptions = new NoiseOptions();
			EraserOptions = new EraserOptions();
			StampOptions = new StampOptions();

			_tools.Add(new ToolIndex(new CameraTool(), null, "T_TOOL_CAMERA", Properties.Resources.eye__1_, Keys.C));
			_tools.Add(new ToolIndex(new PencilTool(), PencilOptions, "T_TOOL_PENCIL", Properties.Resources.pen, Keys.P));
			_tools.Add(new ToolIndex(new EraserTool(), EraserOptions, "T_TOOL_ERASER", Properties.Resources.erase, Keys.E));
			_tools.Add(new ToolIndex(new DropperTool(), null, "T_TOOL_DROPPER", Properties.Resources.pipette, Keys.D));
			_tools.Add(new ToolIndex(new DodgeBurnTool(), DodgeBurnOptions, "T_TOOL_DODGEBURN", Properties.Resources.dodge, Keys.B));
			_tools.Add(new ToolIndex(new DarkenLightenTool(), DarkenLightenOptions, "T_TOOL_DARKENLIGHTEN", Properties.Resources.darkenlighten, Keys.L));
			_tools.Add(new ToolIndex(new FloodFillTool(), FloodFillOptions, "T_TOOL_BUCKET", Properties.Resources.fill_bucket, Keys.F));
			_tools.Add(new ToolIndex(new NoiseTool(), NoiseOptions, "T_TOOL_NOISE", Properties.Resources.noise, Keys.N));
			_tools.Add(new ToolIndex(new StampTool(), StampOptions, "T_TOOL_STAMP", Properties.Resources.stamp_pattern, Keys.M));

			animateToolStripMenuItem.Checked = GlobalSettings.Animate;
			followCursorToolStripMenuItem.Checked = GlobalSettings.FollowCursor;
			grassToolStripMenuItem.Checked = GlobalSettings.Grass;
			ghostHiddenPartsToolStripMenuItem.Checked = GlobalSettings.Ghost;

			alphaCheckerboardToolStripMenuItem.Checked = GlobalSettings.AlphaCheckerboard;
			textureOverlayToolStripMenuItem.Checked = GlobalSettings.TextureOverlay;
			modeToolStripMenuItem1.Checked = GlobalSettings.OnePointEightMode;

			SetCheckbox(VisiblePartFlags.HeadFlag, headToolStripMenuItem);
			SetCheckbox(VisiblePartFlags.ChestFlag, chestToolStripMenuItem);
			SetCheckbox(VisiblePartFlags.LeftArmFlag, leftArmToolStripMenuItem);
			SetCheckbox(VisiblePartFlags.RightArmFlag, rightArmToolStripMenuItem);
			SetCheckbox(VisiblePartFlags.HelmetFlag, helmetToolStripMenuItem);
			SetCheckbox(VisiblePartFlags.LeftLegFlag, leftLegToolStripMenuItem);
			SetCheckbox(VisiblePartFlags.RightLegFlag, rightLegToolStripMenuItem);

			Language.Language useLanguage = null;
			try
			{
				// stage 1 (prelim): if no language, see if our languages contain it
				if (string.IsNullOrEmpty(GlobalSettings.LanguageFile))
					useLanguage = LanguageLoader.FindLanguage((CultureInfo.CurrentUICulture.IsNeutralCulture == false) ? CultureInfo.CurrentUICulture.Parent.Name : CultureInfo.CurrentUICulture.Name);
				// stage 2: load from last used language
				if (useLanguage == null)
					useLanguage = LanguageLoader.FindLanguage(GlobalSettings.LanguageFile);
				// stage 3: use English file, if it exists
				if (useLanguage == null)
					useLanguage = LanguageLoader.FindLanguage("English");
			}
			catch
			{
			}
			finally
			{
				// stage 4: fallback to built-in English file
				if (useLanguage == null)
				{
					MessageBox.Show(this, "For some reason, the default language files were missing or failed to load(did you extract?) - we'll supply you with a base language of English just so you know what you're doing!");
					useLanguage = LanguageLoader.LoadDefault();
				}
			}

			foreach (var lang in LanguageLoader.Languages)
			{
				lang.Item = new ToolStripMenuItem((lang.Culture != null) ? (char.ToUpper(lang.Culture.NativeName[0]) + lang.Culture.NativeName.Substring(1)) : lang.Name);
				lang.Item.Tag = lang;
				lang.Item.Click += new EventHandler(languageToolStripMenuItem_Click);
				languageToolStripMenuItem.DropDownItems.Add(lang.Item);
			}

			for (int i = _tools.Count - 1; i >= 0; --i)
			{
				toolToolStripMenuItem.DropDownItems.Insert(0, _tools[i].MenuItem);
				_tools[i].MenuItem.Click += ToolMenuItemClicked;
				toolStrip1.Items.Insert(toolStrip1.Items.IndexOf(toolStripSeparator1) + 1, _tools[i].Button);
				_tools[i].Button.Click += ToolMenuItemClicked;

				languageProvider1.SetPropertyNames(_tools[i].MenuItem, "Text");
				languageProvider1.SetPropertyNames(_tools[i].Button, "Text");
			}

			InitShortcuts();
			LoadShortcutKeys(GlobalSettings.ShortcutKeys);
			_shortcutEditor.ShortcutExists += new EventHandler<ShortcutExistsEventArgs>(_shortcutEditor_ShortcutExists);
			CurrentLanguage = useLanguage;
			Brushes.LoadBrushes();

			SetSelectedTool(_tools[0]);

			redColorSlider.Renderer = redRenderer = new ColorSliderRenderer(redColorSlider);
			greenColorSlider.Renderer = greenRenderer = new ColorSliderRenderer(greenColorSlider);
			blueColorSlider.Renderer = blueRenderer = new ColorSliderRenderer(blueColorSlider);
			alphaColorSlider.Renderer = alphaRenderer = new AlphaSliderRenderer(alphaColorSlider);

			hueColorSlider.Renderer = hueRenderer = new HueSliderRenderer(hueColorSlider);
			saturationColorSlider.Renderer = saturationRenderer = new SaturationSliderRenderer(saturationColorSlider);
			valueColorSlider.Renderer = valueRenderer = new ValueSliderRenderer(valueColorSlider);

			KeyPreview = true;
			Text = "MCSkin3D v" + Program.Version.ToString();

#if BETA
			Text += " [Beta]";
#endif

			if (!Directory.Exists("Swatches"))
				MessageBox.Show(this, GetLanguageString("B_MSG_DIRMISSING"));

			Directory.CreateDirectory("Swatches");

			foreach (var x in GlobalSettings.SkinDirectories)
				Directory.CreateDirectory(x);
			swatchContainer.AddDirectory("Swatches");

			_updater = new Updater("http://alteredsoftworks.com/mcskin3d/update", Program.Version.ToString());
			_updater.UpdateHandler = new AssemblyVersion();
			_updater.NewVersionAvailable += _updater_NewVersionAvailable;
			_updater.SameVersion += _updater_SameVersion;
			_updater.CheckForUpdate();

			automaticallyCheckForUpdatesToolStripMenuItem.Checked = GlobalSettings.AutoUpdate;

			ModelLoader.LoadModels();

			foreach (var x in ModelLoader.Models)
			{
				ToolStripItemCollection collection = toolStripDropDownButton1.DropDownItems;
				string path = Path.GetDirectoryName(x.Value.File.ToString()).Substring(6);

				while (!string.IsNullOrEmpty(path))
				{
					string sub = path.Substring(1, path.IndexOf('\\', 1) == -1 ? (path.Length - 1) : (path.IndexOf('\\', 1) - 1));

					var subMenu = collection.Find(sub, false);

					if (subMenu.Length == 0)
					{
						var item = ((ToolStripMenuItem)collection.Add(sub));
						item.Name = item.Text;
						collection = item.DropDownItems;
					}
					else
						collection = ((ToolStripMenuItem)subMenu[0]).DropDownItems;

					path = path.Remove(0, sub.Length + 1);
				}

				collection.Add(new ModelToolStripMenuItem(x.Value));
			}

			SetSampleMenuItem(GlobalSettings.Multisamples);

			// set up the GL control
			var mode = new GraphicsMode();
			rendererControl = new GLControl(new GraphicsMode(mode.ColorFormat, mode.Depth, mode.Stencil, GlobalSettings.Multisamples));
			rendererControl.BackColor = System.Drawing.Color.Black;
			rendererControl.Dock = System.Windows.Forms.DockStyle.Fill;
			rendererControl.Location = new System.Drawing.Point(0, 25);
			rendererControl.Name = "rendererControl";
			rendererControl.Size = new System.Drawing.Size(641, 580);
			rendererControl.TabIndex = 4;
			rendererControl.VSync = true;
			rendererControl.Load += new System.EventHandler(this.rendererControl_Load);
			rendererControl.Paint += new System.Windows.Forms.PaintEventHandler(this.rendererControl_Paint);
			rendererControl.MouseDown += new System.Windows.Forms.MouseEventHandler(this.rendererControl_MouseDown);
			rendererControl.MouseMove += new System.Windows.Forms.MouseEventHandler(this.rendererControl_MouseMove);
			rendererControl.MouseUp += new System.Windows.Forms.MouseEventHandler(this.rendererControl_MouseUp);
			rendererControl.MouseLeave += new EventHandler(rendererControl_MouseLeave);
			rendererControl.Resize += new System.EventHandler(this.rendererControl_Resize);
			rendererControl.MouseWheel += new MouseEventHandler(rendererControl_MouseWheel);
			rendererControl.MouseEnter += new EventHandler(rendererControl_MouseEnter);

			splitContainer4.Panel2.Controls.Add(rendererControl);
			rendererControl.BringToFront();

			System.Timers.Timer animTimer = new System.Timers.Timer();
			animTimer.Interval = 22;
			animTimer.Elapsed += new System.Timers.ElapsedEventHandler(animTimer_Elapsed);
			animTimer.Start();

			_animTimer.Elapsed += new System.Timers.ElapsedEventHandler(_animTimer_Elapsed);
			_animTimer.SynchronizingObject = this;

			if (!settingsLoaded)
				MessageBox.Show(this, GetLanguageString("C_SETTINGSFAILED"));

			treeView1.ItemHeight = GlobalSettings.TreeViewHeight;
			treeView1.Scrollable = true;
			splitContainer4.SplitterDistance = 74;

			if (GlobalSettings.OnePointEightMode)
				ModelLoader.InvertBottomFaces();

			this.mLINESIZEToolStripMenuItem.NumericBox.ValueChanged += new System.EventHandler(mLINESIZEToolStripMenuItem_NumericBox_ValueChanged);
			this.mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.ValueChanged += new System.EventHandler(mOVERLAYTEXTSIZEToolStripMenuItem_NumericBox_ValueChanged);
			mLINESIZEToolStripMenuItem.NumericBox.Value = GlobalSettings.DynamicOverlayLineSize;
			mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.Value = GlobalSettings.DynamicOverlayTextSize;

			mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.Minimum = 1;
			mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.Maximum = 16;
			mLINESIZEToolStripMenuItem.NumericBox.Minimum = 1;
			mLINESIZEToolStripMenuItem.NumericBox.Maximum = 16;

			_undoListBox = new UndoRedoPanel();
			_undoListBox.ActionString = "L_UNDOACTIONS";
			languageProvider1.SetPropertyNames(_undoListBox, "ActionString");

			_undoListBox.ListBox.MouseClick += new MouseEventHandler(UndoListBox_MouseClick);

			undoToolStripButton.DropDown = new PopupControl.Popup(_undoListBox);
			undoToolStripButton.DropDownOpening += new EventHandler(undoToolStripButton_DropDownOpening);

			_redoListBox = new UndoRedoPanel();
			_redoListBox.ActionString = "L_REDOACTIONS";
			languageProvider1.SetPropertyNames(_redoListBox, "ActionString");

			_redoListBox.ListBox.MouseClick += new MouseEventHandler(RedoListBox_MouseClick);

			redoToolStripButton.DropDown = new PopupControl.Popup(_redoListBox);
			redoToolStripButton.DropDownOpening += new EventHandler(redoToolStripButton_DropDownOpening);

			undoToolStripButton.DropDown.AutoClose = redoToolStripButton.DropDown.AutoClose = true;

			mINFINITEMOUSEToolStripMenuItem.Checked = GlobalSettings.InfiniteMouse;
			CurrentLanguage = useLanguage;
		}

		void rendererControl_MouseEnter(object sender, EventArgs e)
		{
			rendererControl.Focus();
		}

		void _shortcutEditor_ShortcutExists(object sender, ShortcutExistsEventArgs e)
		{
			//MessageBox.Show(string.Format(GetLanguageString("B_MSG_SHORTCUTEXISTS"), e.ShortcutName, e.OtherName));
		}
		#endregion

		public GLControl Renderer
		{
			get { return rendererControl; }
		}

		public MouseButtons CameraRotate
		{
			get
			{
				if (_selectedTool == _tools[(int)Tools.Camera])
					return MouseButtons.Left;
				else
					return MouseButtons.Right;
			}
		}

		public MouseButtons CameraZoom
		{
			get
			{
				if (_selectedTool == _tools[(int)Tools.Camera])
					return MouseButtons.Right;
				else
					return MouseButtons.Middle;
			}
		}

		public void PerformResetCamera()
		{
			_2dCamOffsetX = 0;
			_2dCamOffsetY = 0;
			_2dZoom = 8;
			_3dZoom = -80;
			_3dRotationX = 180;
			_3dRotationY = 0;
			_3dOffset = Vector3.Zero;

			rendererControl.Invalidate();
		}

		// =====================================================================
		// Updating
		// =====================================================================
		#region Update
		public void Invoke(Action action)
		{
			this.Invoke((Delegate)action);
		}

		void _updater_SameVersion(object sender, EventArgs e)
		{
			this.Invoke(() => MessageBox.Show(this, GetLanguageString("B_MSG_UPTODATE")));
		}

		void _updater_NewVersionAvailable(object sender, EventArgs e)
		{
			this.Invoke(delegate()
			{
				if (MessageBox.Show(this, GetLanguageString("B_MSG_NEWUPDATE"), "Woo!", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
					ShowUpdater();
			});
		}
		#endregion

		// =====================================================================
		// Shortcuts
		// =====================================================================
		#region Shortcuts

		string CompileShortcutKeys()
		{
			string c = "";

			for (int i = 0; i < _shortcutEditor.ShortcutCount; ++i)
			{
				var shortcut = _shortcutEditor.ShortcutAt(i);

				if (i != 0)
					c += "|";

				Keys key = shortcut.Keys & ~Keys.Modifiers;
				Keys modifiers = (Keys)((int)shortcut.Keys - (int)key);

				if (modifiers != 0)
					c += shortcut.SaveName + "=" + key + "+" + modifiers;
				else
					c += shortcut.SaveName + "=" + key;
			}

			return c;
		}

		IShortcutImplementor FindShortcut(string name)
		{
			foreach (var s in _shortcutEditor.Shortcuts)
			{
				if (s.SaveName == name)
					return s;
			}

			return null;
		}

		void LoadShortcutKeys(string s)
		{
			if (string.IsNullOrEmpty(s))
				return; // leave defaults

			var shortcuts = s.Split('|');

			foreach (var shortcut in shortcuts)
			{
				var args = shortcut.Split('=');

				string name = args[0];
				string key;
				string modifiers = "0";

				if (args[1].Contains('+'))
				{
					var mods = args[1].Split('+');

					key = mods[0];
					modifiers = mods[1];
				}
				else
					key = args[1];

				var sh = FindShortcut(name);

				if (sh == null)
					continue;

				sh.Keys = (Keys)Enum.Parse(typeof(Keys), key) | (Keys)Enum.Parse(typeof(Keys), modifiers);
			}
		}

		void InitMenuShortcut(ToolStripMenuItem item, Action callback)
		{
			MenuStripShortcut shortcut = new MenuStripShortcut(item);
			shortcut.Pressed = callback;

			_shortcutEditor.AddShortcut(shortcut);
		}

		void InitMenuShortcut(ToolStripMenuItem item, Keys keys, Action callback)
		{
			MenuStripShortcut shortcut = new MenuStripShortcut(item, keys);
			shortcut.Pressed = callback;

			_shortcutEditor.AddShortcut(shortcut);
		}

		void InitUnlinkedShortcut(string name, Keys defaultKeys, Action callback)
		{
			ShortcutBase shortcut = new ShortcutBase(name, defaultKeys);
			shortcut.Pressed = callback;

			_shortcutEditor.AddShortcut(shortcut);
		}

		void InitControlShortcut(string name, Control control, Keys defaultKeys, Action callback)
		{
			ControlShortcut shortcut = new ControlShortcut(name, defaultKeys, control);
			shortcut.Pressed = callback;

			_shortcutEditor.AddShortcut(shortcut);
		}

		void InitShortcuts()
		{
			// shortcut menus
			InitMenuShortcut(undoToolStripMenuItem, PerformUndo);
			InitMenuShortcut(redoToolStripMenuItem, PerformRedo);
			InitMenuShortcut(perspectiveToolStripMenuItem, () => SetViewMode(ViewMode.Perspective));
			InitMenuShortcut(textureToolStripMenuItem, () => SetViewMode(ViewMode.Orthographic));
			InitMenuShortcut(hybridViewToolStripMenuItem, () => SetViewMode(ViewMode.Hybrid));
			InitMenuShortcut(animateToolStripMenuItem, ToggleAnimation);
			InitMenuShortcut(followCursorToolStripMenuItem, ToggleFollowCursor);
			InitMenuShortcut(grassToolStripMenuItem, ToggleGrass);
			InitMenuShortcut(ghostHiddenPartsToolStripMenuItem, ToggleGhosting);
			InitMenuShortcut(alphaCheckerboardToolStripMenuItem, ToggleAlphaCheckerboard);
			InitMenuShortcut(textureOverlayToolStripMenuItem, ToggleOverlay);
			InitMenuShortcut(offToolStripMenuItem, () => SetTransparencyMode(TransparencyMode.Off));
			InitMenuShortcut(helmetOnlyToolStripMenuItem, () => SetTransparencyMode(TransparencyMode.Helmet));
			InitMenuShortcut(allToolStripMenuItem, () => SetTransparencyMode(TransparencyMode.All));
			InitMenuShortcut(headToolStripMenuItem, () => ToggleVisiblePart(VisiblePartFlags.HeadFlag));
			InitMenuShortcut(helmetToolStripMenuItem, () => ToggleVisiblePart(VisiblePartFlags.HelmetFlag));
			InitMenuShortcut(chestToolStripMenuItem, () => ToggleVisiblePart(VisiblePartFlags.ChestFlag));
			InitMenuShortcut(leftArmToolStripMenuItem, () => ToggleVisiblePart(VisiblePartFlags.LeftArmFlag));
			InitMenuShortcut(rightArmToolStripMenuItem, () => ToggleVisiblePart(VisiblePartFlags.RightArmFlag));
			InitMenuShortcut(leftLegToolStripMenuItem, () => ToggleVisiblePart(VisiblePartFlags.LeftLegFlag));
			InitMenuShortcut(rightLegToolStripMenuItem, () => ToggleVisiblePart(VisiblePartFlags.RightLegFlag));
			InitMenuShortcut(saveToolStripMenuItem, PerformSave);
			InitMenuShortcut(saveAsToolStripMenuItem, PerformSaveAs);
			InitMenuShortcut(saveAllToolStripMenuItem, PerformSaveAll);
			InitMenuShortcut(uploadToolStripMenuItem, PerformUpload);

			foreach (var item in _tools)
				InitMenuShortcut(item.MenuItem, item.DefaultKeys, item.SetMeAsTool);

			// not in the menu
			InitUnlinkedShortcut("S_TOGGLETRANS", Keys.Shift | Keys.U, ToggleTransparencyMode);
			InitUnlinkedShortcut("S_TOGGLEVIEW", Keys.Control | Keys.V, ToggleViewMode);
			InitUnlinkedShortcut("S_SCREENSHOT_CLIP", Keys.Control | Keys.B, TakeScreenshot);
			InitUnlinkedShortcut("S_SCREENSHOT_SAVE", Keys.Control | Keys.Shift | Keys.B, SaveScreenshot);
			InitUnlinkedShortcut("S_DELETE", Keys.Delete, PerformDeleteSkin);
			InitUnlinkedShortcut("S_CLONE", Keys.Control | Keys.C, PerformCloneSkin);
			InitUnlinkedShortcut("S_RENAME", Keys.Control | Keys.R, PerformNameChange);
			InitUnlinkedShortcut("T_TREE_IMPORTHERE", Keys.Control | Keys.I, PerformImportSkin);
			InitUnlinkedShortcut("T_TREE_NEWFOLDER", Keys.Control | Keys.Shift | Keys.N, PerformNewFolder);
			InitUnlinkedShortcut("M_NEWSKIN_HERE", Keys.Control | Keys.Shift | Keys.M, PerformNewSkin);
			InitUnlinkedShortcut("S_COLORSWAP", Keys.S, PerformSwitchColor);
			InitControlShortcut("S_SWATCH_ZOOMIN", swatchContainer.SwatchDisplayer, Keys.Oemplus, PerformSwatchZoomIn);
			InitControlShortcut("S_SWATCH_ZOOMOUT", swatchContainer.SwatchDisplayer, Keys.OemMinus, PerformSwatchZoomOut);
			InitControlShortcut("S_TREEVIEW_ZOOMIN", treeView1, Keys.Control | Keys.Oemplus, PerformTreeViewZoomIn);
			InitControlShortcut("S_TREEVIEW_ZOOMOUT", treeView1, Keys.Control | Keys.OemMinus, PerformTreeViewZoomOut);
			InitUnlinkedShortcut("T_DECRES", Keys.Control | Keys.Shift | Keys.D, PerformDecreaseResolution);
			InitUnlinkedShortcut("T_INCRES", Keys.Control | Keys.Shift | Keys.I, PerformIncreaseResolution);
			InitUnlinkedShortcut("T_RESETCAMERA", Keys.Control | Keys.Shift | Keys.R, PerformResetCamera);
		}

		void PerformSwitchColor()
		{
			if (_secondaryIsFront)
				colorPreview1_Click(null, null);
			else
				colorPreview2_Click(null, null);
		}

		public void SetSelectedTool(ToolIndex index)
		{
			if (_selectedTool != null)
				_selectedTool.MenuItem.Checked = _selectedTool.Button.Checked = false;

			var oldTool = _selectedTool;
			_selectedTool = index;
			index.MenuItem.Checked = index.Button.Checked = true;

			splitContainer4.Panel1.Controls.Clear();

			if (oldTool != null && oldTool.OptionsPanel != null)
				oldTool.OptionsPanel.BoxHidden();
			if (_selectedTool.OptionsPanel != null)
				_selectedTool.OptionsPanel.BoxShown();

			if (_selectedTool.OptionsPanel != null)
			{
				_selectedTool.OptionsPanel.Dock = DockStyle.Fill;
				splitContainer4.Panel1.Controls.Add(_selectedTool.OptionsPanel);
			}

			toolStripStatusLabel1.Text = index.Tool.GetStatusLabelText();
		}

		void ToolMenuItemClicked(object sender, EventArgs e)
		{
			ToolStripItem item = (ToolStripItem)sender;
			SetSelectedTool((ToolIndex)item.Tag);
		}

		void PerformCamera()
		{
		}

		void PerformTreeViewZoomIn()
		{
			treeView1.ZoomIn();
		}

		void PerformTreeViewZoomOut()
		{
			treeView1.ZoomOut();
		}

		void PerformSwatchZoomOut()
		{
			swatchContainer.ZoomOut();
		}

		void PerformSwatchZoomIn()
		{
			swatchContainer.ZoomIn();
		}

		bool PerformShortcut(Keys key, Keys modifiers)
		{
			foreach (var shortcut in _shortcutEditor.Shortcuts)
			{
				if (shortcut.CanEvaluate() && (shortcut.Keys & ~Keys.Modifiers) == key &&
					(shortcut.Keys & ~(shortcut.Keys & ~Keys.Modifiers)) == modifiers)
				{
					shortcut.Pressed();
					return true;
				}
			}

			return false;
		}
		#endregion

		// =====================================================================
		// Overrides
		// =====================================================================
		#region Overrides
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (textBox1.ContainsFocus == false && labelEditTextBox.ContainsFocus == false)
				if (PerformShortcut(e.KeyCode & ~Keys.Modifiers, e.Modifiers))
				{
					e.Handled = true;
					return;
				}

			base.OnKeyDown(e);
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (RecursiveNodeIsDirty(treeView1.Nodes))
			{
				if (MessageBox.Show(this, GetLanguageString("C_UNSAVED"), GetLanguageString("C_UNSAVED_CAPTION"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.No)
				{
					e.Cancel = true;
					return;
				}
			}

			_updater.Abort();
			GlobalSettings.ShortcutKeys = CompileShortcutKeys();

			swatchContainer.SaveSwatches();

			if (_newSkinDirs != null)
				GlobalSettings.SkinDirectories = _newSkinDirs;

			GlobalSettings.Save();
		}

		TreeNode _tempToSelect;
		void RecurseAddDirectories(string path, TreeNodeCollection nodes, List<Skin> skins)
		{
			var di = new DirectoryInfo(path);

			foreach (var file in di.GetFiles("*.png", SearchOption.TopDirectoryOnly))
			{
				var skin = new Skin(file);
				nodes.Add(skin);

				if (_tempToSelect == null)
					_tempToSelect = skin;
				else if (GlobalSettings.LastSkin == skin.File.ToString())
					_tempToSelect = skin;

				skins.Add(skin);
			}

			foreach (var dir in di.GetDirectories())
			{
				if ((dir.Attributes & FileAttributes.Hidden) != 0)
					continue;

				FolderNode folderNode = new FolderNode(dir.FullName);
				RecurseAddDirectories(dir.FullName, folderNode.Nodes, skins);
				nodes.Add(folderNode);
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			SetTransparencyMode(GlobalSettings.Transparency);
			SetViewMode(_currentViewMode);

			rendererControl.MakeCurrent();

			List<Skin> skins = new List<Skin>();
			treeView1.BeginUpdate();

			if (Editor.HasOneRoot)
				RecurseAddDirectories(Editor.RootFolderString, treeView1.Nodes, skins);
			else
			{
				foreach (var x in GlobalSettings.SkinDirectories)
				{
					FolderNode folder = new FolderNode(x);
					RecurseAddDirectories(x, folder.Nodes, skins);
					treeView1.Nodes.Add(folder);
				}
			}

			foreach (var s in skins)
				s.SetImages();

			treeView1.EndUpdate();
			treeView1.SelectedNode = _tempToSelect;

			SetColor(ColorManager.FromRGBA(255, 255, 255, 255));
			SetVisibleParts();

			toolToolStripMenuItem.DropDown.Closing += DontCloseMe;
			modeToolStripMenuItem.DropDown.Closing += DontCloseMe;
			threeDToolStripMenuItem.DropDown.Closing += DontCloseMe;
			twoDToolStripMenuItem.DropDown.Closing += DontCloseMe;
			transparencyModeToolStripMenuItem.DropDown.Closing += DontCloseMe;
			visiblePartsToolStripMenuItem.DropDown.Closing += DontCloseMe;
			mSHAREDToolStripMenuItem.DropDown.Closing += DontCloseMe;

			optionsToolStripMenuItem.DropDown.Closing += (sender, args) =>
			{
				if (modeToolStripMenuItem1.Selected && (args.CloseReason == ToolStripDropDownCloseReason.ItemClicked || args.CloseReason == ToolStripDropDownCloseReason.Keyboard))
					args.Cancel = true;
			};

			if (Screen.PrimaryScreen.BitsPerPixel != 32)
			{
				MessageBox.Show(Editor.GetLanguageString("B_MSG_PIXELFORMAT"), Editor.GetLanguageString("B_CAP_SORRY"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				Application.Exit();
			}

			if (Environment.CurrentDirectory.StartsWith(Environment.ExpandEnvironmentVariables("%temp%")))
			{
				MessageBox.Show(GetLanguageString("M_TEMP"));
				Application.Exit();
			}

			//new GUIDPicker("..\\update_guids.txt").ShowDialog();
		}

		void DontCloseMe(object sender, ToolStripDropDownClosingEventArgs e)
		{
			if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked ||
				e.CloseReason == ToolStripDropDownCloseReason.Keyboard)
				e.Cancel = true;
		}

		#endregion

		// =====================================================================
		// Private functions
		// =====================================================================
		#region Private Functions
		// Utility function, sets a tool strip checkbox item's state if the flag is present
		void SetCheckbox(VisiblePartFlags flag, ToolStripMenuItem checkbox)
		{
			if ((GlobalSettings.ViewFlags & flag) != 0)
				checkbox.Checked = true;
			else
				checkbox.Checked = false;
		}

		public ColorManager SelectedColor
		{
			get { return (_secondaryIsFront) ? _secondaryColor : _primaryColor; }
			set { SetColor(value); }
		}

		public ColorManager UnselectedColor
		{
			get { return (!_secondaryIsFront) ? _secondaryColor : _primaryColor; }
			set
			{
				if (_secondaryIsFront)
				{
					if (swatchContainer.InEditMode)
					{
						if (swatchContainer.SwatchDisplayer.HasPrimaryColor)
							swatchContainer.SwatchDisplayer.PrimaryColor = value.RGB;
					}

					SetColor(colorPreview1, ref _primaryColor, value);
				}
				else
				{
					if (swatchContainer.InEditMode)
					{
						if (swatchContainer.SwatchDisplayer.HasSecondaryColor)
							swatchContainer.SwatchDisplayer.SecondaryColor = value.RGB;
					}

					SetColor(colorPreview2, ref _secondaryColor, value);
				}
			}
		}

		void UseToolOnViewport(int x, int y, bool begin = false)
		{
			if (_lastSkin == null)
				return;
			
			if (_isValidPick)
			{
				Skin skin = _lastSkin;

				ColorGrabber currentSkin = new ColorGrabber(GlobalDirtiness.CurrentSkin, skin.Width, skin.Height);
				currentSkin.Load();

				if (_selectedTool.Tool.MouseMoveOnSkin(ref currentSkin, skin, _pickPosition.X, _pickPosition.Y))
				{
					SetCanSave(true);
					skin.Dirty = true;
					currentSkin.Save();
				}
			}

			rendererControl.Invalidate();
		}

		#region File uploading (FIXME: REMOVE)
		public static Exception HttpUploadFile(string url, string file, string paramName, string contentType, Dictionary<string, byte[]> nvc, CookieContainer cookies)
		{
			//log.Debug(string.Format("Uploading {0} to {1}", file, url));
			string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

			HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
			wr.ContentType = "multipart/form-data; boundary=" + boundary;
			wr.Method = "POST";
			wr.KeepAlive = true;
			wr.CookieContainer = cookies;
			wr.Credentials = System.Net.CredentialCache.DefaultCredentials;
			wr.Timeout = 10000;

			Stream rs = wr.GetRequestStream();

			string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
			foreach (var kvp in nvc)
			{
				rs.Write(boundarybytes, 0, boundarybytes.Length);
				string formitem = string.Format(formdataTemplate, kvp.Key, Encoding.ASCII.GetString(kvp.Value));
				byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
				rs.Write(formitembytes, 0, formitembytes.Length);
			}
			rs.Write(boundarybytes, 0, boundarybytes.Length);

			string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
			string header = string.Format(headerTemplate, paramName, Path.GetFileName(file), contentType);
			byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
			rs.Write(headerbytes, 0, headerbytes.Length);

			FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
			byte[] buffer = new byte[4096];
			int bytesRead = 0;
			while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
			{
				rs.Write(buffer, 0, bytesRead);
			}
			fileStream.Close();

			byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
			rs.Write(trailer, 0, trailer.Length);
			rs.Close();

			WebResponse wresp = null;
			Exception ret = null;
			try
			{
				wresp = wr.GetResponse();
				Stream stream2 = wresp.GetResponseStream();
				StreamReader reader2 = new StreamReader(stream2);
				//log.Debug(string.Format("File uploaded, server response is: {0}", reader2.ReadToEnd()));
			}
			catch (Exception ex)
			{
				//log.Error("Error uploading file", ex);
				if (wresp != null)
				{
					wresp.Close();
					wresp = null;
				}

				ret = ex;
			}
			finally
			{
				wr = null;
			}

			return ret;
		}

		public enum ErrorCodes
		{
			Succeeded,
			TimeOut,
			WrongCredentials,
			Unknown
		}

		class ErrorReturn
		{
			public ErrorCodes Code;
			public Exception Exception;
			public string ReportedError;
		}

		void UploadThread(object param)
		{
			var parms = (object[])param;
			ErrorReturn error = (ErrorReturn)parms[3];

			error.Code = ErrorCodes.Succeeded;
			error.Exception = null;
			error.ReportedError = null;

			try
			{
				CookieContainer cookies = new CookieContainer();
				var request = (HttpWebRequest)HttpWebRequest.Create("http://www.minecraft.net/login");
				request.CookieContainer = cookies;
				request.Timeout = 10000;
				var response = request.GetResponse();
				StreamReader sr = new StreamReader(response.GetResponseStream());
				var text = sr.ReadToEnd();

				var match = Regex.Match(text, @"<input type=""hidden"" name=""authenticityToken"" value=""(.*?)"">");
				string authToken = null;
				if (match.Success)
					authToken = match.Groups[1].Value;

				if (authToken == null)
					return;

				sr.Dispose();

				response.Close();

				string requestTemplate = @"authenticityToken={0}&redirect=http%3A%2F%2Fwww.minecraft.net%2Fprofile&username={1}&password={2}";
				string requestContent = string.Format(requestTemplate, authToken, parms[0].ToString(), parms[1].ToString());
				var inBytes = Encoding.UTF8.GetBytes(requestContent);

				// craft the login request
				request = (HttpWebRequest)HttpWebRequest.Create("https://www.minecraft.net/login");
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.CookieContainer = cookies;
				request.ContentLength = inBytes.Length;
				request.Timeout = 10000;

				using (Stream dataStream = request.GetRequestStream())
					dataStream.Write(inBytes, 0, inBytes.Length);

				response = request.GetResponse();
				sr = new StreamReader(response.GetResponseStream());
				text = sr.ReadToEnd();

				match = Regex.Match(text, @"<p class=""error"">([\w\W]*?)</p>");

				sr.Dispose();
				response.Close();

				if (match.Success)
				{
					error.ReportedError = match.Groups[1].Value.Trim();
					error.Code = ErrorCodes.WrongCredentials;
				}
				else
				{
					var dict = new Dictionary<string, byte[]>();
					dict.Add("authenticityToken", Encoding.ASCII.GetBytes(authToken));
					if ((error.Exception = HttpUploadFile("http://www.minecraft.net/profile/skin", parms[2].ToString(), "skin", "image/png", dict, cookies)) != null)
						error.Code = ErrorCodes.Unknown;
				}
			}
			catch (Exception ex)
			{
				error.Exception = ex;
			}
			finally
			{
				Invoke((Action)delegate() { _pleaseWaitForm.Close(); });
			}
		}

		Thread _uploadThread;
		Login login = new Login();

		void PerformUpload()
		{
			if (_lastSkin == null)
				return;

			if (_lastSkin.Width != 64 || _lastSkin.Height != 32)
			{
				MessageBox.Show(this, GetLanguageString("B_MSG_UPLOADRES"));
				return;
			}

			login.Username = GlobalSettings.LastUsername;
			login.Password = GlobalSettings.LastPassword;

			bool dialogRes = true;
			bool didShowDialog = false;

			if ((ModifierKeys & Keys.Shift) != 0 || !GlobalSettings.RememberMe || !GlobalSettings.AutoLogin)
			{
				login.Remember = GlobalSettings.RememberMe;
				login.AutoLogin = GlobalSettings.AutoLogin;
				dialogRes = login.ShowDialog() == System.Windows.Forms.DialogResult.OK;
				didShowDialog = true;
			}

			if (!dialogRes)
				return;

			_pleaseWaitForm = new PleaseWait();
			_pleaseWaitForm.FormClosed += new FormClosedEventHandler(_pleaseWaitForm_FormClosed);

			_uploadThread = new Thread(UploadThread);
			ErrorReturn ret = new ErrorReturn();
			_uploadThread.Start(new object[] { login.Username, login.Password, _lastSkin.File.FullName, ret });

			_pleaseWaitForm.DialogResult = DialogResult.OK;
			_pleaseWaitForm.languageProvider1.LanguageChanged(CurrentLanguage);
			_pleaseWaitForm.ShowDialog();
			_uploadThread = null;

			if (ret.ReportedError != null)
				MessageBox.Show(this, GetLanguageString("B_MSG_UPLOADERROR") + "\r\n" + ret.ReportedError, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			else if (ret.Exception != null)
				MessageBox.Show(this, GetLanguageString("B_MSG_UPLOADERROR") + "\r\n" + ret.Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			else if (_pleaseWaitForm.DialogResult != DialogResult.Abort)
			{
				MessageBox.Show(this, GetLanguageString("B_MSG_UPLOADSUCCESS"), "Woo!", MessageBoxButtons.OK, MessageBoxIcon.Information);
				GlobalSettings.LastSkin = _lastSkin.File.ToString();
				treeView1.Invalidate();
			}

			if (didShowDialog)
			{
				GlobalSettings.RememberMe = login.Remember;
				GlobalSettings.AutoLogin = login.AutoLogin;

				if (GlobalSettings.RememberMe == false)
					GlobalSettings.LastUsername = GlobalSettings.LastPassword = "";
				else
				{
					GlobalSettings.LastUsername = login.Username;
					GlobalSettings.LastPassword = login.Password;
				}
			}
		}

		void _pleaseWaitForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			_uploadThread.Abort();
		}
		#endregion

		void ToggleAnimation()
		{
			animateToolStripMenuItem.Checked = !animateToolStripMenuItem.Checked;
			GlobalSettings.Animate = animateToolStripMenuItem.Checked;

			Invalidate();
		}

		void ToggleFollowCursor()
		{
			followCursorToolStripMenuItem.Checked = !followCursorToolStripMenuItem.Checked;
			GlobalSettings.FollowCursor = followCursorToolStripMenuItem.Checked;

			Invalidate();
		}

		void ToggleGrass()
		{
			grassToolStripMenuItem.Checked = !grassToolStripMenuItem.Checked;
			GlobalSettings.Grass = grassToolStripMenuItem.Checked;

			rendererControl.Invalidate();
		}

		void ToggleGhosting()
		{
			ghostHiddenPartsToolStripMenuItem.Checked = !ghostHiddenPartsToolStripMenuItem.Checked;
			GlobalSettings.Ghost = ghostHiddenPartsToolStripMenuItem.Checked;

			rendererControl.Invalidate();
		}

		void Perform10Mode()
		{
			ModelLoader.InvertBottomFaces();

			modeToolStripMenuItem1.Checked = !modeToolStripMenuItem1.Checked;
			GlobalSettings.OnePointEightMode = modeToolStripMenuItem1.Checked;

			rendererControl.Invalidate();
		}

		#region Skin Management
		void ImportSkin(string fileName, string folderLocation, TreeNode parentNode)
		{
			var name = Path.GetFileNameWithoutExtension(fileName);

			while (File.Exists(folderLocation + name + ".png"))
				name += " (New)";

			File.Copy(fileName, folderLocation + name + ".png");

			Skin skin = new Skin(folderLocation + name + ".png");

			if (parentNode != null)
			{
				if (!(parentNode is Skin))
					parentNode.Nodes.Add(skin);
				else
					parentNode.GetParentCollection().Add(skin);
			}
			else
				treeView1.Nodes.Add(skin);

			skin.SetImages();
			treeView1.SelectedNode = skin;
		}

		public static string GetFolderForNode(TreeNode node)
		{
			if (node == null)
				return Editor.RootFolderString;
			else if (node is Skin)
			{
				if (node.Parent == null)
				{
					if (Editor.HasOneRoot)
						return Editor.RootFolderString;

					throw new Exception();
				}
				else
					return GetFolderForNode(node.Parent);
			}
			else if (node is FolderNode)
			{
				FolderNode folder = (FolderNode)node;
				return folder.Directory.FullName;
			}

			throw new Exception();
		}

		public static string GetFolderLocationForNode(TreeNode node)
		{
			string folderLocation = "";

			if (node != null)
			{
				if (!(node is Skin))
					folderLocation = GetFolderForNode(node) + '\\';
				else if (node.Parent != null)
					folderLocation = GetFolderForNode(node.Parent) + '\\';
			}

			if (Editor.HasOneRoot)
				folderLocation = Editor.RootFolderString + '\\' + folderLocation;

			return folderLocation;
		}

		public static void GetFolderLocationAndCollectionForNode(TreeView treeView, TreeNode _rightClickedNode, out string folderLocation, out TreeNodeCollection collection)
		{
			folderLocation = "";
			collection = treeView.Nodes;

			if (_rightClickedNode != null)
			{
				if (!(_rightClickedNode is Skin))
				{
					folderLocation = GetFolderForNode(_rightClickedNode) + '\\';
					collection = _rightClickedNode.Nodes;
				}
				else if (_rightClickedNode.Parent != null)
				{
					folderLocation = GetFolderForNode(_rightClickedNode.Parent) + '\\';
					collection = _rightClickedNode.Parent.Nodes;
				}
			}

			if (Editor.HasOneRoot)
				folderLocation = Editor.RootFolderString + '\\' + folderLocation;
		}

		void ImportSkins(string[] fileName, TreeNode parentNode)
		{
			var folderLocation = GetFolderLocationForNode(parentNode);

			foreach (var f in fileName)
				ImportSkin(f, folderLocation, parentNode);
		}

		void PerformImportSkin()
		{
			using (var ofd = new OpenFileDialog())
			{
				ofd.Filter = "Minecraft Skins|*.png";
				ofd.Multiselect = true;
				ofd.RestoreDirectory = true;

				if (ofd.ShowDialog() == DialogResult.OK)
				{
					if (_rightClickedNode == null)
						_rightClickedNode = treeView1.SelectedNode;

					ImportSkins(ofd.FileNames, _rightClickedNode);
				}
			}
		}

		void PerformNewFolder()
		{
			string folderLocation;
			TreeNodeCollection collection;

			if (_rightClickedNode == null || _rightClickedNode.Parent == null)
				_rightClickedNode = treeView1.SelectedNode;

			GetFolderLocationAndCollectionForNode(treeView1, _rightClickedNode, out folderLocation, out collection);

			string newFolderName = "New Folder";

			while (Directory.Exists(folderLocation + newFolderName))
				newFolderName = newFolderName.Insert(0, Editor.GetLanguageString("C_NEW"));

			Directory.CreateDirectory(folderLocation + newFolderName);
			var newNode = new FolderNode(folderLocation + newFolderName);
			collection.Add(newNode);

			newNode.EnsureVisible();
			treeView1.SelectedNode = newNode;
			treeView1.Invalidate();

			PerformNameChange();
		}

		void PerformNewSkin()
		{
			string folderLocation;
			TreeNodeCollection collection;

			if (_rightClickedNode == null)
				_rightClickedNode = treeView1.SelectedNode;

			GetFolderLocationAndCollectionForNode(treeView1, _rightClickedNode, out folderLocation, out collection);

			string newSkinName = "New Skin";

			while (File.Exists(folderLocation + newSkinName + ".png"))
				newSkinName = newSkinName.Insert(0, Editor.GetLanguageString("C_NEW"));

			using (Bitmap bmp = new Bitmap(64, 32))
			{
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Color.FromArgb(0, 255, 255, 255));

					g.FillRectangle(System.Drawing.Brushes.White, 0, 0, 32, 32);
					g.FillRectangle(System.Drawing.Brushes.White, 32, 16, 32, 16);
				}

				bmp.Save(folderLocation + newSkinName + ".png");
			}

			Skin newSkin = new Skin(folderLocation + newSkinName + ".png");
			collection.Add(newSkin);
			newSkin.SetImages();

			newSkin.EnsureVisible();
			treeView1.SelectedNode = newSkin;
			treeView1.Invalidate();

			PerformNameChange();
		}

		void RecursiveDeleteSkins(TreeNode node)
		{
			foreach (TreeNode sub in node.Nodes)
			{
				if (!(sub is Skin))
					RecursiveDeleteSkins(sub);
				else
				{
					Skin skin = (Skin)sub;

					if (_lastSkin == skin)
						_lastSkin = null;

					skin.Dispose();
				}
			}

			Directory.Delete(GetFolderForNode(node), true);
		}

		void PerformDeleteSkin()
		{
			if (treeView1.SelectedNode is Skin)
			{
				if (MessageBox.Show(this, GetLanguageString("B_MSG_DELETESKIN"), GetLanguageString("B_CAP_QUESTION"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
				{
					Skin skin = (Skin)treeView1.SelectedNode;

					skin.Delete();
					skin.Remove();
					skin.Dispose();

					treeView1_AfterSelect(treeView1, new TreeViewEventArgs(treeView1.SelectedNode));

					Invalidate();
				}
			}
			else
			{
				if (MessageBox.Show(this, GetLanguageString("B_MSG_DELETEFOLDER"), GetLanguageString("B_CAP_QUESTION"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.Yes)
				{
					DirectoryInfo folder = new DirectoryInfo(GetFolderForNode(treeView1.SelectedNode));

					RecursiveDeleteSkins(treeView1.SelectedNode);

					treeView1.SelectedNode.Remove();
					treeView1_AfterSelect(treeView1, new TreeViewEventArgs(treeView1.SelectedNode));
					Invalidate();
				}
			}
		}

		void PerformCloneSkin()
		{
			if (treeView1.SelectedNode == null ||
				!(treeView1.SelectedNode is Skin))
				return;

			Skin skin = (Skin)treeView1.SelectedNode;
			string newName = skin.Name;
			string newFileName;

			do
			{
				newName += " - Copy";
				newFileName = skin.Directory.FullName + '\\' + newName + ".png";
			} while (File.Exists(newFileName));

			File.Copy(skin.File.FullName, newFileName);
			Skin newSkin = new Skin(newFileName);

			skin.GetParentCollection().Add(newSkin);

			newSkin.SetImages();
		}

		TreeNode _currentlyEditing = null;
		void PerformNameChange()
		{
			if (treeView1.SelectedNode != null)
			{
				_currentlyEditing = treeView1.SelectedNode;

				if (_currentlyEditing is Skin)
					labelEditTextBox.Text = ((Skin)_currentlyEditing).Name;
				else
					labelEditTextBox.Text = _currentlyEditing.Text;

				labelEditTextBox.Show();
				labelEditTextBox.Location = new Point(treeView1.SelectedNode.Bounds.Location.X + 24 + (treeView1.SelectedNode.Level * 1), treeView1.Location.Y + treeView1.SelectedNode.Bounds.Location.Y + 4);
				labelEditTextBox.Size = new System.Drawing.Size(treeView1.Width - labelEditTextBox.Location.X - 20, labelEditTextBox.Height);
				labelEditTextBox.BringToFront();
				labelEditTextBox.Focus();
			}
		}
		#endregion

		private void DoneEditingNode(string newName, TreeNode _currentlyEditing)
		{
			labelEditTextBox.Hide();

			if (_currentlyEditing is Skin)
			{
				Skin skin = (Skin)_currentlyEditing;

				if (skin.Name == newName)
					return;

				if (skin.ChangeName(newName) == false)
					System.Media.SystemSounds.Beep.Play();
			}
			else
			{
				string folderName = _currentlyEditing.Text;

				var folder = new DirectoryInfo(GetFolderForNode(_currentlyEditing));
				var newFolder = new DirectoryInfo(((_currentlyEditing.Parent != null) ? (GetFolderForNode(_currentlyEditing.Parent) + '\\' + newName) : newName));

				if (folderName == newName)
					return;

				if (Directory.Exists(newFolder.FullName))
				{
					System.Media.SystemSounds.Beep.Play();
					return;
				}

				folder.MoveTo(newFolder.FullName);
				_currentlyEditing.Text = _currentlyEditing.Name = newFolder.Name;
			}
		}

		#region Saving
		void SetCanSave(bool value)
		{
			saveToolStripButton.Enabled = saveToolStripMenuItem.Enabled = value;
			CheckUndo();

			//treeView1.Invalidate();
		}

		void PerformSaveAs()
		{
			var skin = _lastSkin;

			ColorGrabber grabber = new ColorGrabber(GlobalDirtiness.CurrentSkin, skin.Width, skin.Height);
			grabber.Load();

			Bitmap b = new Bitmap(skin.Width, skin.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			var locked = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			unsafe
			{
				fixed (void *inPixels = grabber.Array)
				{
					void *outPixels = locked.Scan0.ToPointer();

					int *inInt = (int*)inPixels;
					int *outInt = (int*)outPixels;

					for (int y = 0; y < b.Height; ++y)
						for (int x = 0; x < b.Width; ++x)
						{
							var color = Color.FromArgb((*inInt >> 24) & 0xFF, (*inInt >> 0) & 0xFF, (*inInt >> 8) & 0xFF, (*inInt >> 16) & 0xFF);
							*outInt = color.ToArgb();

							inInt++;
							outInt++;
						}
				}
			}

			b.UnlockBits(locked);

			using (SaveFileDialog sfd = new SaveFileDialog())
			{
				sfd.Filter = "Skin Image|*.png";
				sfd.RestoreDirectory = true;

				if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					b.Save(sfd.FileName);
			}

			b.Dispose();
		}

		void PerformSaveSkin(Skin s)
		{
			rendererControl.MakeCurrent();

			s.CommitChanges((s == _lastSkin) ? GlobalDirtiness.CurrentSkin : s.GLImage, true);
		}

		bool RecursiveNodeIsDirty(TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				if (node is Skin)
				{
					Skin skin = (Skin)node;

					if (skin.Dirty)
						return true;
				}
				else
					if (RecursiveNodeIsDirty(node.Nodes))
						return true;
			}

			return false;
		}

		void RecursiveNodeSave(TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				if (node is Skin)
				{
					Skin skin = (Skin)node;

					if (skin.Dirty)
						PerformSaveSkin(skin);
				}
				else
					RecursiveNodeSave(node.Nodes);
			}
		}

		void PerformSaveAll()
		{
			RecursiveNodeSave(treeView1.Nodes);
			treeView1.Invalidate();
		}

		void PerformSave()
		{
			Skin skin = _lastSkin;

			if (!skin.Dirty)
				return;

			SetCanSave(false);
			PerformSaveSkin(skin);
			treeView1.Invalidate();
		}
		#endregion

		void BeginUndo()
		{
			if (!_currentUndoBuffer.CanUndo)
				return;

			rendererControl.MakeCurrent();
		}

		void DoUndo()
		{
			if (!_currentUndoBuffer.CanUndo)
				throw new Exception();

			_currentUndoBuffer.Undo();
		}

		void EndUndo()
		{
			undoToolStripMenuItem.Enabled = undoToolStripButton.Enabled = _currentUndoBuffer.CanUndo;
			redoToolStripMenuItem.Enabled = redoToolStripButton.Enabled = _currentUndoBuffer.CanRedo;

			SetCanSave(_lastSkin.Dirty = true);

			rendererControl.Invalidate();
		}

		void PerformUndo()
		{
			BeginUndo();
			DoUndo();
			EndUndo();
		}

		void UndoListBox_MouseClick(object sender, MouseEventArgs e)
		{
			undoToolStripButton.DropDown.Close();

			BeginUndo();
			for (int i = 0; i <= _undoListBox.ListBox.HighestItemSelected; ++i)
				DoUndo();
			EndUndo();
		}

		void undoToolStripButton_DropDownOpening(object sender, EventArgs e)
		{
			_undoListBox.ListBox.Items.Clear();

			foreach (var x in _currentUndoBuffer.UndoList)
				_undoListBox.ListBox.Items.Insert(0, x.Action);
		}

		void BeginRedo()
		{
			if (!_currentUndoBuffer.CanRedo)
				return;

			rendererControl.MakeCurrent();
		}

		void DoRedo()
		{
			_currentUndoBuffer.Redo();
		}

		void EndRedo()
		{
			SetCanSave(_lastSkin.Dirty = true);

			rendererControl.Invalidate();
		}

		void PerformRedo()
		{
			BeginRedo();
			DoRedo();
			EndRedo();
		}

		void RedoListBox_MouseClick(object sender, MouseEventArgs e)
		{
			redoToolStripButton.DropDown.Close();

			BeginRedo();
			for (int i = 0; i <= _redoListBox.ListBox.HighestItemSelected; ++i)
				DoRedo();
			EndRedo();
		}

		private void redoToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			PerformRedo();
		}

		void redoToolStripButton_DropDownOpening(object sender, EventArgs e)
		{
			_redoListBox.ListBox.Items.Clear();

			foreach (var x in _currentUndoBuffer.RedoList)
				_redoListBox.ListBox.Items.Insert(0, x.Action);
		}

		Paril.Controls.Color.ColorPreview SelectedColorPreview
		{
			get { return (_secondaryIsFront) ? colorPreview2 : colorPreview1; }
		}

		void SetColor(Control colorPreview, ref ColorManager currentColor, ColorManager newColor)
		{
			currentColor = newColor;
			colorPreview.ForeColor = currentColor.RGB;

			if (colorPreview != SelectedColorPreview)
				return;

			_skipColors = true;
			colorPick1.CurrentHSV = currentColor.HSV;

			redNumericUpDown.Value = currentColor.RGB.R;
			greenNumericUpDown.Value = currentColor.RGB.G;
			blueNumericUpDown.Value = currentColor.RGB.B;
			alphaNumericUpDown.Value = currentColor.RGB.A;

			hueNumericUpDown.Value = currentColor.HSV.H;
			saturationNumericUpDown.Value = currentColor.HSV.S;
			valueNumericUpDown.Value = currentColor.HSV.V;

			redRenderer.StartColor = Color.FromArgb(255, 0, currentColor.RGB.G, currentColor.RGB.B);
			greenRenderer.StartColor = Color.FromArgb(255, currentColor.RGB.R, 0, currentColor.RGB.B);
			blueRenderer.StartColor = Color.FromArgb(255, currentColor.RGB.R, currentColor.RGB.G, 0);

			redRenderer.EndColor = Color.FromArgb(255, 255, currentColor.RGB.G, currentColor.RGB.B);
			greenRenderer.EndColor = Color.FromArgb(255, currentColor.RGB.R, 255, currentColor.RGB.B);
			blueRenderer.EndColor = Color.FromArgb(255, currentColor.RGB.R, currentColor.RGB.G, 255);

			alphaRenderer.EndColor = Color.FromArgb(255, currentColor.RGB.ToColor());

			hueRenderer.CurrentColor = currentColor;
			saturationRenderer.CurrentColor = currentColor;
			valueRenderer.CurrentColor = currentColor;

			redColorSlider.Value = currentColor.RGB.R;
			greenColorSlider.Value = currentColor.RGB.G;
			blueColorSlider.Value = currentColor.RGB.B;
			alphaColorSlider.Value = currentColor.RGB.A;

			hueColorSlider.Value = currentColor.HSV.H;
			saturationColorSlider.Value = currentColor.HSV.S;
			valueColorSlider.Value = currentColor.HSV.V;

			if (!_editingHex)
				textBox1.Text = string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", newColor.RGB.R, newColor.RGB.G, newColor.RGB.B, newColor.RGB.A);

			_skipColors = false;
		}

		void SetColor(ColorManager c)
		{
			if (_secondaryIsFront)
			{
				if (swatchContainer.InEditMode)
				{
					if (swatchContainer.SwatchDisplayer.HasSecondaryColor)
						swatchContainer.SwatchDisplayer.SecondaryColor = c.RGB;
				}

				SetColor(colorPreview2, ref _secondaryColor, c);
			}
			else
			{
				if (swatchContainer.InEditMode)
				{
					if (swatchContainer.SwatchDisplayer.HasPrimaryColor)
						swatchContainer.SwatchDisplayer.PrimaryColor = c.RGB;
				}

				SetColor(colorPreview1, ref _primaryColor, c);
			}
		}

		private void colorPick1_HSVChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromHSVA(colorPick1.CurrentHSV.H, colorPick1.CurrentHSV.S, colorPick1.CurrentHSV.V, SelectedColor.HSV.A));
		}

		void SetViewMode(ViewMode newMode)
		{
			perspectiveToolStripButton.Checked = orthographicToolStripButton.Checked = hybridToolStripButton.Checked = false;
			perspectiveToolStripMenuItem.Checked = textureToolStripMenuItem.Checked = hybridViewToolStripMenuItem.Checked = false;
			_currentViewMode = newMode;

			switch (_currentViewMode)
			{
			case ViewMode.Orthographic:
				orthographicToolStripButton.Checked = true;
				textureToolStripMenuItem.Checked = true;
				break;
			case ViewMode.Perspective:
				perspectiveToolStripButton.Checked = true;
				perspectiveToolStripMenuItem.Checked = true;
				break;
			case ViewMode.Hybrid:
				hybridToolStripButton.Checked = true;
				hybridViewToolStripMenuItem.Checked = true;
				break;
			}

			rendererControl.Invalidate();
		}

		void SetTransparencyMode(TransparencyMode trans)
		{
			offToolStripMenuItem.Checked = helmetOnlyToolStripMenuItem.Checked = allToolStripMenuItem.Checked = false;
			GlobalSettings.Transparency = trans;

			switch (GlobalSettings.Transparency)
			{
			case TransparencyMode.Off:
				offToolStripMenuItem.Checked = true;
				break;
			case TransparencyMode.Helmet:
				helmetOnlyToolStripMenuItem.Checked = true;
				break;
			case TransparencyMode.All:
				allToolStripMenuItem.Checked = true;
				break;
			}

			rendererControl.Invalidate();
		}

		ToolStripMenuItem[] _toggleMenuItems;
		ToolStripButton[] _toggleButtons;
		void SetVisibleParts()
		{
			if (_toggleMenuItems == null)
			{
				_toggleMenuItems = new ToolStripMenuItem[] { headToolStripMenuItem, helmetToolStripMenuItem, chestToolStripMenuItem, leftArmToolStripMenuItem, rightArmToolStripMenuItem, leftLegToolStripMenuItem, rightLegToolStripMenuItem };
				_toggleButtons = new ToolStripButton[] { toggleHeadToolStripButton, toggleHelmetToolStripButton, toggleChestToolStripButton, toggleLeftArmToolStripButton, toggleRightArmToolStripButton, toggleLeftLegToolStripButton, toggleRightLegToolStripButton };
			}

			for (int i = 0; i < _toggleButtons.Length; ++i)
				_toggleMenuItems[i].Checked = _toggleButtons[i].Checked = ((GlobalSettings.ViewFlags & (VisiblePartFlags)(1 << i)) != 0);
		}

		void ToggleVisiblePart(VisiblePartFlags flag)
		{
			GlobalSettings.ViewFlags ^= flag;

			bool hasNow = (GlobalSettings.ViewFlags & flag) != 0;

			ToolStripMenuItem item = null;
			ToolStripButton itemButton = null;

			// TODO: ugly
			switch (flag)
			{
			case VisiblePartFlags.HeadFlag:
				item = headToolStripMenuItem;
				itemButton = toggleHeadToolStripButton;
				break;
			case VisiblePartFlags.HelmetFlag:
				item = helmetToolStripMenuItem;
				itemButton = toggleHelmetToolStripButton;
				break;
			case VisiblePartFlags.ChestFlag:
				item = chestToolStripMenuItem;
				itemButton = toggleChestToolStripButton;
				break;
			case VisiblePartFlags.LeftArmFlag:
				item = leftArmToolStripMenuItem;
				itemButton = toggleLeftArmToolStripButton;
				break;
			case VisiblePartFlags.RightArmFlag:
				item = rightArmToolStripMenuItem;
				itemButton = toggleRightArmToolStripButton;
				break;
			case VisiblePartFlags.LeftLegFlag:
				item = leftLegToolStripMenuItem;
				itemButton = toggleLeftLegToolStripButton;
				break;
			case VisiblePartFlags.RightLegFlag:
				item = rightLegToolStripMenuItem;
				itemButton = toggleRightLegToolStripButton;
				break;
			}

			item.Checked = hasNow;
			itemButton.Checked = hasNow;

			rendererControl.Invalidate();
		}

		void ToggleAlphaCheckerboard()
		{
			GlobalSettings.AlphaCheckerboard = !GlobalSettings.AlphaCheckerboard;
			alphaCheckerboardToolStripMenuItem.Checked = GlobalSettings.AlphaCheckerboard;
			rendererControl.Invalidate();
		}

		void ToggleOverlay()
		{
			GlobalSettings.TextureOverlay = !GlobalSettings.TextureOverlay;
			textureOverlayToolStripMenuItem.Checked = GlobalSettings.TextureOverlay;
			rendererControl.Invalidate();
		}

		void ToggleTransparencyMode()
		{
			switch (GlobalSettings.Transparency)
			{
			case TransparencyMode.Off:
				SetTransparencyMode(TransparencyMode.Helmet);
				break;
			case TransparencyMode.Helmet:
				SetTransparencyMode(TransparencyMode.All);
				break;
			case TransparencyMode.All:
				SetTransparencyMode(TransparencyMode.Off);
				break;
			}
		}

		void ToggleViewMode()
		{
			switch (_currentViewMode)
			{
			case ViewMode.Orthographic:
				SetViewMode(ViewMode.Perspective);
				break;
			case ViewMode.Perspective:
				SetViewMode(ViewMode.Hybrid);
				break;
			case ViewMode.Hybrid:
				SetViewMode(ViewMode.Orthographic);
				break;
			}
		}

		#region Screenshots
		Bitmap CopyScreenToBitmap()
		{
			rendererControl.MakeCurrent();
			_screenshotMode = true;
			rendererControl_Paint(null, null);
			_screenshotMode = false;

			Bitmap b = new Bitmap(rendererControl.Width, rendererControl.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			int[] pixels = new int[rendererControl.Width * rendererControl.Height];
			GL.ReadPixels(0, 0, rendererControl.Width, rendererControl.Height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

			var locked = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			unsafe
			{
				fixed (void *inPixels = pixels)
				{
					void *outPixels = locked.Scan0.ToPointer();

					int *inInt = (int*)inPixels;
					int *outInt = (int*)outPixels;

					for (int y = 0; y < b.Height; ++y)
						for (int x = 0; x < b.Width; ++x)
						{
							var color = Color.FromArgb((*inInt >> 24) & 0xFF, (*inInt >> 0) & 0xFF, (*inInt >> 8) & 0xFF, (*inInt >> 16) & 0xFF);
							*outInt = color.ToArgb();

							inInt++;
							outInt++;
						}
				}
			}

			b.UnlockBits(locked);
			b.RotateFlip(RotateFlipType.RotateNoneFlipY);

			return b;
		}

		void TakeScreenshot()
		{
			Clipboard.SetImage(CopyScreenToBitmap());
		}

		void SaveScreenshot()
		{
			using (SaveFileDialog sfd = new SaveFileDialog())
			{
				sfd.Filter = "PNG Image|*.png";
				sfd.RestoreDirectory = true;

				if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					using (var bmp = CopyScreenToBitmap())
						bmp.Save(sfd.FileName);
				}
			}
		}
		#endregion
		#endregion

		void rendererControl_Load(object sender, EventArgs e)
		{
			rendererControl.Invalidate();
			GL.ClearColor(GlobalSettings.BackgroundColor);

			GL.Enable(EnableCap.Texture2D);
			GL.ShadeModel(ShadingModel.Smooth);                        // Enable Smooth Shading
			GL.ClearDepth(1.0f);                         // Depth Buffer Setup
			GL.Enable(EnableCap.DepthTest);                        // Enables Depth Testing
			GL.DepthFunc(DepthFunction.Lequal);                         // The Type Of Depth Testing To Do
			GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);          // Really Nice Perspective Calculations
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Modulate);
			GL.Enable(EnableCap.CullFace);
			GL.CullFace(CullFaceMode.Front);

			_toolboxUpNormal = new TextureGL(Properties.Resources.buttong);
			_toolboxUpHover = new TextureGL(Properties.Resources.buttong_2);
			_toolboxDownNormal = new TextureGL(Properties.Resources.buttong_down);
			_toolboxDownHover = new TextureGL(Properties.Resources.buttong_down2);

			_font = new TextureGL(Properties.Resources.tinyfont);
			_font.SetMipmapping(false);
			_font.SetRepeat(false);

			_grassTop = new TextureGL("grass.png");
			_grassTop.SetMipmapping(false);
			_grassTop.SetRepeat(true);

			_dynamicOverlay = new BackgroundImage("Dynamic", "Dynamic", null);
			_dynamicOverlay.Item = mDYNAMICOVERLAYToolStripMenuItem;
			_backgrounds.Add(_dynamicOverlay);

			foreach (var file in Directory.GetFiles("Overlays", "*.png"))
			{
				try
				{
					var image = new TextureGL(file);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);

					_backgrounds.Add(new BackgroundImage(file, Path.GetFileNameWithoutExtension(file), image));
				}
				catch
				{
					MessageBox.Show(this, string.Format(GetLanguageString("B_MSG_OVERLAYERROR"), file));
				}
			}

			int index = 0;
			foreach (var b in _backgrounds)
			{
				ToolStripMenuItem item = (b.Item == null) ? new ToolStripMenuItem(b.Name) : b.Item;
				b.Item = item;

				if (b.Path == GlobalSettings.LastBackground)
				{
					item.Checked = true;
					_selectedBackground = index;
				}

				item.Click += item_Clicked;
				item.Tag = index++;

				if (!backgroundsToolStripMenuItem.DropDownItems.Contains(item))
					backgroundsToolStripMenuItem.DropDownItems.Add(item);
			}

			_previewPaint = new TextureGL();
			GlobalDirtiness.CurrentSkin = new TextureGL();
			_alphaTex = new TextureGL();

			unsafe
			{
				byte[] arra = new byte[64 * 32];
				_previewPaint.Upload(arra, 64, 32);
				_previewPaint.SetMipmapping(false);
				_previewPaint.SetRepeat(false);

				GlobalDirtiness.CurrentSkin.Upload(arra, 64, 32);
				GlobalDirtiness.CurrentSkin.SetMipmapping(false);
				GlobalDirtiness.CurrentSkin.SetRepeat(false);

				arra = new byte[4 * 4 * 4];
				fixed (byte* texData = arra)
				{
					byte *d = texData;

					for (int y = 0; y < 4; ++y)
						for (int x = 0; x < 4; ++x)
						{
							bool dark = ((x + (y & 1)) & 1) == 1;

							if (dark)
								*((int*)d) = (80 << 0) | (80 << 8) | (80 << 16) | (255 << 24);
							else
								*((int*)d) = (127 << 0) | (127 << 8) | (127 << 16) | (255 << 24);
							d += 4;
						}
				}

				_alphaTex.Upload(arra, 4, 4);
				_alphaTex.SetMipmapping(false);
				_alphaTex.SetRepeat(true);
			}

			if (GL.GetString(StringName.Extensions).Contains("GL_EXT_vertex_array"))
				_renderer = new ClientArrayRenderer();
			else
				_renderer = new ImmediateRenderer();
		}

		void item_Clicked(object sender, EventArgs e)
		{
			var item = (ToolStripMenuItem)sender;
			GlobalSettings.LastBackground = item.Text;
			_backgrounds[_selectedBackground].Item.Checked = false;
			_selectedBackground = (int)item.Tag;
			item.Checked = true;
		}


		Texture GetPaintTexture(int width, int height)
		{
			if (!_charPaintSizes.ContainsKey(new Size(width, height)))
			{
				var tex = new TextureGL();

				int[] arra = new int[width * height];
				unsafe
				{
					fixed (int* texData = arra)
					{
						int *d = texData;

						for (int y = 0; y < height; ++y)
							for (int x = 0; x < width; ++x)
							{
								*d = ((y * width) + x) | (255 << 24);
								d++;
							}
					}
				}

				tex.Upload(arra, width, height);
				tex.SetMipmapping(false);
				tex.SetRepeat(false);

				_charPaintSizes.Add(new Size(width, height), tex);

				return tex;
			}

			return _charPaintSizes[new Size(width, height)];
		}

		void DrawSkinnedRectangle
			(float x, float y, float z, float width, float length, float height,
			int topSkinX, int topSkinY, int topSkinW, int topSkinH,
			Texture texture, int skinW = 64, int skinH = 32)
		{
			texture.Bind();

			GL.Begin(BeginMode.Quads);

			width /= 2;
			length /= 2;
			height /= 2;

			float tsX = (float)topSkinX / skinW;
			float tsY = (float)topSkinY / skinH;
			float tsW = (float)topSkinW / skinW;
			float tsH = (float)topSkinH / skinH;

			GL.TexCoord2(tsX, tsY + tsH - 0.00005); GL.Vertex3(x - width, y + length, z + height);          // Bottom Right Of The Quad (Top)
			GL.TexCoord2(tsX + tsW - 0.00005, tsY + tsH - 0.00005); GL.Vertex3(x + width, y + length, z + height);          // Bottom Left Of The Quad (Top)
			GL.TexCoord2(tsX + tsW - 0.00005, tsY); GL.Vertex3(x + width, y + length, z - height);          // Top Left Of The Quad (Top)
			GL.TexCoord2(tsX, tsY); GL.Vertex3(x - width, y + length, z - height);          // Top Right Of The Quad (Top)

			GL.End();
		}

		void DrawCharacter2D(Texture font, byte c, float xOfs, float yOfs, float width, float height)
		{
			font.Bind();

			float tx = (((c - 32) % 16) * 8) / 128.0f;
			float ty = (((c - 32) / 16) * 8) / 64.0f;
			const float txw = 8.0f / 128.0f;
			const float txh = 8.0f / 64.0f;

			GL.Begin(BeginMode.Quads);
			GL.TexCoord2(tx, ty);
			GL.Vertex2(xOfs, yOfs);
			GL.TexCoord2(tx + txw, ty);
			GL.Vertex2(xOfs + width, yOfs);
			GL.TexCoord2(tx + txw, ty + txh);
			GL.Vertex2(xOfs + width, height + yOfs);
			GL.TexCoord2(tx, ty + txh);
			GL.Vertex2(xOfs, height + yOfs);
			GL.End();
		}

		void DrawString(Texture font, string s, float spacing, float size)
		{
			float x = 0;
			foreach (var c in s)
			{
				DrawCharacter2D(font, (byte)c, x, 0, size, size);
				x += spacing;
			}

			TextureGL.Unbind();
		}

		void DrawStringWithinRectangle(Texture font, RectangleF rect, string s, float spacing, float size)
		{
			float x = rect.X;
			float y = rect.Y;
			foreach (var c in s)
			{
				DrawCharacter2D(font, (byte)c, x, y, size, size);
				x += spacing;

				if ((x + spacing) > rect.X + rect.Width)
				{
					x = rect.X;
					y += spacing;
				}

				if ((y + spacing) > rect.Y + rect.Height)
					break;
			}

			TextureGL.Unbind();
		}

		void DrawPlayer2D(Texture tex, Skin skin, bool pickView)
		{
			if (!pickView && GlobalSettings.AlphaCheckerboard)
			{
				_alphaTex.Bind();

				GL.Begin(BeginMode.Quads);
				GL.TexCoord2(0, 0); GL.Vertex2(0, 0);
				GL.TexCoord2(_currentViewport.Width / 32.0f, 0); GL.Vertex2(_currentViewport.Width, 0);
				GL.TexCoord2(_currentViewport.Width / 32.0f, _currentViewport.Height / 32.0f); GL.Vertex2(_currentViewport.Width, _currentViewport.Height);
				GL.TexCoord2(0, _currentViewport.Height / 32.0f); GL.Vertex2(0, _currentViewport.Height);
				GL.End();
			}

			if (skin != null)
				tex.Bind();

			GL.PushMatrix();

			GL.Translate((_2dCamOffsetX), (_2dCamOffsetY), 0);
			GL.Translate((_currentViewport.Width / 2) + -_2dCamOffsetX, (_currentViewport.Height / 2) + -_2dCamOffsetY, 0);
			GL.Scale(_2dZoom, _2dZoom, 1);

			if (pickView)
				GL.Disable(EnableCap.Blend);
			else
				GL.Enable(EnableCap.Blend);

			GL.Translate((_2dCamOffsetX), (_2dCamOffsetY), 0);
			if (skin != null)
			{
				float w = skin.Width;
				float h = skin.Height;
				GL.Begin(BeginMode.Quads);
				GL.TexCoord2(0, 0); GL.Vertex2(-(CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2));
				GL.TexCoord2(1, 0); GL.Vertex2((CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2));
				GL.TexCoord2(1, 1); GL.Vertex2((CurrentModel.DefaultWidth / 2), (CurrentModel.DefaultHeight / 2));
				GL.TexCoord2(0, 1); GL.Vertex2(-(CurrentModel.DefaultWidth / 2), (CurrentModel.DefaultHeight / 2));
				GL.End();
			}

			if (!pickView)
			{
				// Grid test
				TextureGL.Unbind();

				GL.Color3(Color.Black);
				GL.PushMatrix();
				GL.Translate(-(CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2), 0);
				GL.Begin(BeginMode.Lines);


				GL.Vertex2(0, 0);
				GL.Vertex2(0, CurrentModel.DefaultHeight);
				GL.Vertex2(1, 0);
				GL.Vertex2(1, CurrentModel.DefaultHeight);

				GL.Vertex2(0, 0);
				GL.Vertex2(CurrentModel.DefaultWidth, 0);
				GL.Vertex2(0, 1);
				GL.Vertex2(CurrentModel.DefaultWidth, 1);


				GL.End();
				GL.PopMatrix();
				// Grid test end

				if (GlobalSettings.TextureOverlay && skin != null)
				{
					if (_backgrounds[_selectedBackground] == _dynamicOverlay)
					{
						GL.PushMatrix();
						GL.Translate(-(CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2), 0);

						var stub = (GlobalSettings.DynamicOverlayLineSize / _2dZoom);
						var one = (1.0f / _2dZoom);

						List<RectangleF> done = new List<RectangleF>();
						foreach (var mesh in CurrentModel.Meshes)
						{
							foreach (var face in mesh.Faces)
							{
								var toint = face.TexCoordsToFloat((int)CurrentModel.DefaultWidth, (int)CurrentModel.DefaultHeight);

								if (toint.Width == 0 ||
									toint.Height == 0)
									continue;
								if (done.Contains(toint))
									continue;

								done.Add(toint);

								GL.Color4(GlobalSettings.DynamicOverlayLineColor);
								GL.Begin(BeginMode.Quads);
								GL.Vertex2(toint.X, toint.Y);
								GL.Vertex2(toint.X + toint.Width, toint.Y);
								GL.Vertex2(toint.X + toint.Width, toint.Y + stub);
								GL.Vertex2(toint.X, toint.Y + stub);

								GL.Vertex2(toint.X, toint.Y);
								GL.Vertex2(toint.X + stub, toint.Y);
								GL.Vertex2(toint.X + stub, toint.Y + toint.Height);
								GL.Vertex2(toint.X, toint.Y + toint.Height);

								GL.Vertex2(toint.X + toint.Width + one, toint.Y);
								GL.Vertex2(toint.X + toint.Width + one, toint.Y + toint.Height);
								GL.Vertex2(toint.X + toint.Width + one - stub, toint.Y + toint.Height);
								GL.Vertex2(toint.X + toint.Width + one - stub, toint.Y);

								GL.Vertex2(toint.X, toint.Y + toint.Height + one);
								GL.Vertex2(toint.X, toint.Y + toint.Height + one - stub);
								GL.Vertex2(toint.X + toint.Width, toint.Y + toint.Height + one - stub);
								GL.Vertex2(toint.X + toint.Width, toint.Y + toint.Height + one);
								GL.End();
								GL.Color4(Color.White);

								GL.Color4(GlobalSettings.DynamicOverlayTextColor);
								DrawStringWithinRectangle(_font, toint, mesh.Name + " " + Model.SideFromNormal(face.Normal), (6 * GlobalSettings.DynamicOverlayTextSize) / _2dZoom, (8.0f * GlobalSettings.DynamicOverlayTextSize) / _2dZoom);
								GL.Color4(Color.White);
							}
						}

						GL.PopMatrix();
					}
					else
					{
						_backgrounds[_selectedBackground].GLImage.Bind();

						GL.Begin(BeginMode.Quads);
						GL.TexCoord2(0, 0); GL.Vertex2(-(CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2));
						GL.TexCoord2(1, 0); GL.Vertex2((CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2));
						GL.TexCoord2(1, 1); GL.Vertex2((CurrentModel.DefaultWidth / 2), (CurrentModel.DefaultHeight / 2));
						GL.TexCoord2(0, 1); GL.Vertex2(-(CurrentModel.DefaultWidth / 2), (CurrentModel.DefaultHeight / 2));
						GL.End();
					}
				}
			}

			GL.PopMatrix();

			GL.Disable(EnableCap.Blend);
		}

		public static Model CurrentModel
		{
			get { return MainForm._lastSkin == null ? null : MainForm._lastSkin.Model; }
		}

		void DrawPlayer(Texture tex, Skin skin, bool pickView)
		{
			TextureGL.Unbind();
			bool grass = !pickView && grassToolStripMenuItem.Checked;

			var clPt = rendererControl.PointToClient(Cursor.Position);
			var x = clPt.X - (_currentViewport.Width / 2);
			var y = clPt.Y - (_currentViewport.Height / 2);

			if (!pickView && GlobalSettings.Transparency == TransparencyMode.All)
				GL.Enable(EnableCap.Blend);
			else
				GL.Disable(EnableCap.Blend);

			if (grass)
				DrawSkinnedRectangle(0, GrassY, 0, 1024, 0, 1024, 0, 0, 1024, 1024, _grassTop, 16, 16);

			Vector3 helmetRotate = (GlobalSettings.FollowCursor) ? new Vector3((float)y / 25, (float)x / 25, 0) : Vector3.Zero;
			double sinAnim = (GlobalSettings.Animate) ? Math.Sin(_animationTime) : 0;

			// add meshes
			if (GlobalSettings.RenderBenchmark)
				_compileTimer.Start();

			if (CurrentModel != null)
			foreach (var mesh in CurrentModel.Meshes)
			{
				if ((GlobalSettings.ViewFlags & mesh.Part) == 0 &&
					!(GlobalSettings.Ghost && !pickView))
					continue;

				var newMesh = mesh;

				newMesh.Texture = tex;

				if (mesh.FollowCursor && GlobalSettings.FollowCursor)
					newMesh.Rotate += helmetRotate;

				if ((GlobalSettings.ViewFlags & mesh.Part) == 0 && GlobalSettings.Ghost && !pickView)
				{
					foreach (var f in mesh.Faces)
						for (int i = 0; i < f.Colors.Length; ++i)
							f.Colors[i] = new Color4(1, 1, 1, 0.25f);
				}
				else
				{
					foreach (var f in mesh.Faces)
						for (int i = 0; i < f.Colors.Length; ++i)
							f.Colors[i] = Color4.White;
				}

				if (GlobalSettings.Animate && mesh.RotateFactor != 0)
					newMesh.Rotate += new Vector3((float)sinAnim * mesh.RotateFactor, 0, 0);

				_renderer.AddMesh(newMesh);
			}

			if (GlobalSettings.RenderBenchmark)
				_compileTimer.Stop();

			if (!pickView)
				_renderer.Render();
			else
				_renderer.RenderWithoutTransparency();
		}

		Point _pickPosition = new Point(-1, -1);
		bool _isValidPick = false;

		void SetPreview()
		{
			if (_lastSkin == null)
			{
				ColorGrabber preview = new ColorGrabber(_previewPaint, 64, 32);
				preview.Save();
			}
			else
			{
				Skin skin = _lastSkin;

				ColorGrabber currentSkin = new ColorGrabber(GlobalDirtiness.CurrentSkin, skin.Width, skin.Height);

				var pick = GetPick(_mousePoint.X, _mousePoint.Y, ref _pickPosition);

				//if (pick)
				{
					currentSkin.Load();
					if (_selectedTool.Tool.RequestPreview(ref currentSkin, skin, _pickPosition.X, _pickPosition.Y))
					{
						currentSkin.Texture = _previewPaint;
						currentSkin.Save();
					}
					else
					{
						currentSkin.Texture = _previewPaint;
						currentSkin.Save();
					}
				}
				/*else
				{
					currentSkin.Texture = _lastSkin.GLImage;
					currentSkin.Load();
					currentSkin.Texture = _previewPaint;
					currentSkin.Save();
				}*/
			}
		}

		public bool GetPick(int x, int y, ref Point hitPixel)
		{
			if (x < 0 || y < 0
				|| x > rendererControl.Width || y > rendererControl.Height)
			{
				hitPixel = new Point(-1, -1);
				return false;
			}

			rendererControl.MakeCurrent();

			GL.ClearColor(Color.White);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			GL.ClearColor(GlobalSettings.BackgroundColor);

			var skin = _lastSkin;

			if (skin == null)
				return false;

			if (_currentViewMode == ViewMode.Perspective)
			{
				Setup3D(new Rectangle(0, 0, rendererControl.Width, rendererControl.Height));
				DrawPlayer(GetPaintTexture(skin.Width, skin.Height), skin, true);
			}
			else if (_currentViewMode == ViewMode.Orthographic)
			{
				Setup2D(new Rectangle(0, 0, rendererControl.Width, rendererControl.Height));
				DrawPlayer2D(GetPaintTexture(skin.Width, skin.Height), skin, true);
			}
			else
			{
				int halfHeight = (int)Math.Ceiling(rendererControl.Height / 2.0f);

				Setup3D(new Rectangle(0, 0, rendererControl.Width, halfHeight));
				DrawPlayer(GetPaintTexture(skin.Width, skin.Height), skin, true);

				Setup2D(new Rectangle(0, halfHeight, rendererControl.Width, halfHeight));
				DrawPlayer2D(GetPaintTexture(skin.Width, skin.Height), skin, true);
			}

			GL.Flush();

			byte[] pixel = new byte[4];

			GL.ReadPixels(x, rendererControl.Height - y, 1, 1,
				PixelFormat.Rgb, PixelType.UnsignedByte, pixel);

			uint pixVal = BitConverter.ToUInt32(pixel, 0);

			if (pixVal != 0xFFFFFF)
			{
				hitPixel = new Point((int)(pixVal % skin.Width), (int)(pixVal / skin.Width));
				return true;
			}

			hitPixel = new Point(-1, -1);
			return false;
		}

		static bool _ddh = false;
		void animTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			if (!_ddh)
			{
				new DragDropHelper();
				_ddh = true;
			}

			_animationTime += 0.24f;
			rendererControl.Invalidate();
		}

		void rendererControl_MouseWheel(object sender, MouseEventArgs e)
		{
			CheckMouse(e.Y);

			if (_currentViewMode == ViewMode.Perspective || (_currentViewMode == ViewMode.Hybrid && _mouseIn3D))
				_3dZoom += e.Delta / 50;
			else
				_2dZoom += e.Delta / 50;
			if (_2dZoom < 0.25f) _2dZoom = 0.25f;

			rendererControl.Invalidate();
		}

		void DrawGLToolbar()
		{
			// 2D
			Setup2D(new Rectangle(0, 0, rendererControl.Width, rendererControl.Height));
			TextureGL.Unbind();
			GL.Enable(EnableCap.Blend);

			float halfWidth = rendererControl.Width / 2.0f;
			float halfImgWidth = 56.0f / 2.0f;

			var rect = new RectangleF(halfWidth - halfImgWidth, 0, halfImgWidth * 2, 22);

			var img = (splitContainer4.SplitterDistance == 0) ? _toolboxDownNormal : _toolboxUpNormal;

			if (rect.Contains(_mousePoint))
				GL.Color4((byte)255, (byte)255, (byte)255, (byte)255);
			else
				GL.Color4((byte)255, (byte)255, (byte)255, (byte)64);

			img.Bind();

			const float widSep = 56.0f / 64.0f;
			const float heiSep = 22.0f / 32.0f;

			GL.Begin(BeginMode.Quads);
			GL.TexCoord2(0, 0); GL.Vertex2(halfWidth - halfImgWidth, -1);
			GL.TexCoord2(widSep, 0); GL.Vertex2(halfWidth + halfImgWidth, -1);
			GL.TexCoord2(widSep, heiSep); GL.Vertex2(halfWidth + halfImgWidth, 21);
			GL.TexCoord2(0, heiSep); GL.Vertex2(halfWidth - halfImgWidth, 21);
			GL.End();
		}

		static bool _screenshotMode = false;

		public static Stopwatch _renderTimer = new Stopwatch();
		public static Stopwatch _sortTimer = new Stopwatch();
		public static Stopwatch _batchTimer = new Stopwatch();
		public static Stopwatch _compileTimer = new Stopwatch();

		void rendererControl_Paint(object sender, PaintEventArgs e)
		{
			if (GlobalSettings.RenderBenchmark)
			{
				_sortTimer.Reset();
				_batchTimer.Reset();
				_compileTimer.Reset();

				_renderTimer.Reset();
				_renderTimer.Start();
			}

			_mousePoint = rendererControl.PointToClient(MousePosition);

			rendererControl.MakeCurrent();
			SetPreview();

			GL.ClearColor(GlobalSettings.BackgroundColor);
			GL.Color4((byte)255, (byte)255, (byte)255, (byte)255);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			var skin = (Skin)_lastSkin;

			GL.PushMatrix();

			if (_currentViewMode == ViewMode.Perspective)
			{
				Setup3D(new Rectangle(0, 0, rendererControl.Width, rendererControl.Height));
				DrawPlayer(_previewPaint, skin, false);
			}
			else if (_currentViewMode == ViewMode.Orthographic)
			{
				Setup2D(new Rectangle(0, 0, rendererControl.Width, rendererControl.Height));
				DrawPlayer2D(_previewPaint, skin, false);
			}
			else
			{
				int halfHeight = (int)Math.Ceiling(rendererControl.Height / 2.0f);

				Setup3D(new Rectangle(0, 0, rendererControl.Width, halfHeight));
				DrawPlayer(_previewPaint, skin, false);

				Setup2D(new Rectangle(0, halfHeight, rendererControl.Width, halfHeight));
				DrawPlayer2D(_previewPaint, skin, false);
			}

			GL.PopMatrix();

			if (GlobalSettings.RenderBenchmark)
			{
				GL.PushMatrix();
				GL.Enable(EnableCap.Blend);
				_renderTimer.Stop();

				Setup2D(new Rectangle(0, 0, rendererControl.Width, rendererControl.Height));

				DrawString(_font, "Compile Time: " + _compileTimer.Elapsed.ToString(), 6, 8);
				GL.Translate(0, 8, 0);
				DrawString(_font, "Batch Time: " + _batchTimer.Elapsed.ToString(), 6, 8);
				GL.Translate(0, 8, 0);
				DrawString(_font, "Sort Time: " + _sortTimer.Elapsed.ToString(), 6, 8);
				GL.Translate(0, 8, 0);
				DrawString(_font, "Total Frame Time: " + _renderTimer.Elapsed.ToString(), 6, 8);
				GL.PopMatrix();
			}

			if (!_screenshotMode)
				DrawGLToolbar();

			if (!_screenshotMode)
				rendererControl.SwapBuffers();
		}

		Rectangle _currentViewport;

		Matrix4 viewMatrix, cameraMatrix;
		void Setup3D(Rectangle viewport)
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.Viewport(viewport);
			var mat = OpenTK.Matrix4d.Perspective(45, (double)viewport.Width / (double)viewport.Height, 0.01, 100000);
			GL.MultMatrix(ref mat);

			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();

			_currentViewport = viewport;

			Bounds3 vec = Bounds3.EmptyBounds;
			var allBounds = Bounds3.EmptyBounds;
			int count = 0;

			if (CurrentModel != null)
			foreach (var mesh in CurrentModel.Meshes)
			{
				allBounds += mesh.Bounds;

				if ((GlobalSettings.ViewFlags & mesh.Part) != 0)
				{
					vec += mesh.Bounds;
					count++;
				}
			}

			GrassY = allBounds.Maxs.Y;
			var center = vec.Center;

			// FIXME: calculate these only on change
			viewMatrix =
				Matrix4.CreateTranslation(-center.X + _3dOffset.X, -center.Y + _3dOffset.Y, -center.Z + _3dOffset.Z) *
				Matrix4.CreateFromAxisAngle(new Vector3(0, -1, 0), MathHelper.DegreesToRadians(_3dRotationY)) *
				Matrix4.CreateFromAxisAngle(new Vector3(1, 0, 0), MathHelper.DegreesToRadians(_3dRotationX)) *
				Matrix4.CreateTranslation(0, 0, _3dZoom);

			GL.LoadMatrix(ref viewMatrix);

			cameraMatrix = viewMatrix;
			cameraMatrix.Invert();

			CameraPosition = Vector3.TransformPosition(Vector3.Zero, cameraMatrix);
		}

		void Setup2D(Rectangle viewport)
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.Viewport(viewport);
			GL.Ortho(0, viewport.Width, viewport.Height, 0, -1, 1);
			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();

			_currentViewport = viewport;
		}

		void rendererControl_Resize(object sender, EventArgs e)
		{
			rendererControl.MakeCurrent();

			rendererControl.Invalidate();
		}

		System.Timers.Timer _animTimer = new System.Timers.Timer(25);
		bool _opening = false;

		void _animTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			if (_opening)
			{
				splitContainer4.SplitterDistance += splitContainer4.SplitterIncrement;

				if (splitContainer4.SplitterDistance >= 74)
				{
					splitContainer4.SplitterDistance = 74;
					_animTimer.Stop();
				}
			}
			else
			{
				if ((splitContainer4.SplitterDistance - splitContainer4.SplitterIncrement) <= splitContainer4.Panel1MinSize)
				{
					splitContainer4.SplitterDistance = splitContainer4.Panel1MinSize;
					_animTimer.Stop();
					return;
				}

				splitContainer4.SplitterDistance -= splitContainer4.SplitterIncrement;
			}
		}

		public float ToolScale
		{
			get
			{
				const float baseSize = 200.0f;

				return baseSize / rendererControl.Size.Width;
			}
		}

		public void RotateView(Point delta, float factor)
		{
			if (_currentViewMode == ViewMode.Perspective || (_currentViewMode == ViewMode.Hybrid && _mouseIn3D))
			{
				_3dRotationY += (float)(delta.X * ToolScale) * factor;
				_3dRotationX += (float)(delta.Y * ToolScale) * factor;
			}
			else
			{
				_2dCamOffsetX += delta.X / _2dZoom;
				_2dCamOffsetY += delta.Y / _2dZoom;
			}
		}

		public void ScaleView(Point delta, float factor)
		{
			if (_currentViewMode == ViewMode.Perspective || (_currentViewMode == ViewMode.Hybrid && _mouseIn3D))
			{
				_3dZoom += (float)(-delta.Y * ToolScale) * factor;
			}
			else
			{
				_2dZoom += -delta.Y / 25.0f;

				if (_2dZoom < 0.25f)
					_2dZoom = 0.25f;
			}
		}

		bool _mouseIn3D = false;
		void CheckMouse(int y)
		{
			if (y > (rendererControl.Height / 2))
				_mouseIn3D = true;
			else
				_mouseIn3D = false;
		}

		void rendererControl_MouseDown(object sender, MouseEventArgs e)
		{
			Skin skin = _lastSkin;

			if (skin == null)
				return;

			CheckMouse(e.Y);

			float halfWidth = rendererControl.Width / 2.0f;
			float halfImgWidth = 56.0f / 2.0f;

			var rect = new RectangleF(halfWidth - halfImgWidth, 0, halfImgWidth * 2, 22);

			_mousePoint = e.Location;

			if (rect.Contains(e.Location))
			{
				if (splitContainer4.SplitterDistance == 0)
					_opening = true;
				else
					_opening = false;

				_animTimer.Start();
				return;
			}

			_mouseIsDown = true;
			//_isValidPick = GetPick(e.X, e.Y, ref _pickPosition);

			if (e.Button == MouseButtons.Left)
			{
				if (_isValidPick)
					_selectedTool.Tool.BeginClick(_lastSkin, _pickPosition, e);
				else
					_selectedTool.Tool.BeginClick(_lastSkin, new Point(-1, -1), e);
				UseToolOnViewport(e.X, e.Y);
			}
			else
				_tools[(int)Tools.Camera].Tool.BeginClick(_lastSkin, Point.Empty, e);
		}

		void rendererControl_MouseMove(object sender, MouseEventArgs e)
		{
			Skin skin = _lastSkin;

			if (skin == null)
				return;

			_isValidPick = GetPick(e.X, e.Y, ref _pickPosition);

			if (_mouseIsDown)
			{
				if (e.Button == MouseButtons.Left)
				{
					_selectedTool.Tool.MouseMove(_lastSkin, e);
					UseToolOnViewport(e.X, e.Y);
				}
				else
					_tools[(int)Tools.Camera].Tool.MouseMove(_lastSkin, e);

				rendererControl.Invalidate();
			}

			_mousePoint = e.Location;
		}

		void rendererControl_MouseUp(object sender, MouseEventArgs e)
		{
			Skin skin = _lastSkin;

			if (skin == null)
				return;

			if (_mouseIsDown)
			{
				ColorGrabber currentSkin = new ColorGrabber();

				if (e.Button == MouseButtons.Left)
				{
					currentSkin = new ColorGrabber(GlobalDirtiness.CurrentSkin, skin.Width, skin.Height);
					currentSkin.Load();

					if (_selectedTool.Tool.EndClick(ref currentSkin, skin, e))
					{
						SetCanSave(true);
						skin.Dirty = true;
						treeView1.Invalidate();
						currentSkin.Save();
					}
				}
				else
					_tools[(int)Tools.Camera].Tool.EndClick(ref currentSkin, _lastSkin, e);
			}

			_mouseIsDown = false;
			treeView1.Invalidate();
		}

		public void CheckUndo()
		{
			if (_currentUndoBuffer != null)
			{
				undoToolStripMenuItem.Enabled = undoToolStripButton.Enabled = _currentUndoBuffer.CanUndo;
				redoToolStripMenuItem.Enabled = redoToolStripButton.Enabled = _currentUndoBuffer.CanRedo;
			}
			else
			{
				undoToolStripMenuItem.Enabled = undoToolStripButton.Enabled = false;
				redoToolStripMenuItem.Enabled = redoToolStripButton.Enabled = false;
			}
		}

		void rendererControl_MouseLeave(object sender, EventArgs e)
		{
			_mousePoint = new Point(-1, -1);
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (_skipListbox || treeView1.SelectedNode == _lastSkin ||
				!(e.Node is Skin))
				return;

			rendererControl.MakeCurrent();

			if (_lastSkin != null && treeView1.SelectedNode != _lastSkin)
			{
				// Copy over the current changes to the tex stored in the skin.
				// This allows us to pick up where we left off later, without undoing any work.
				_lastSkin.CommitChanges(GlobalDirtiness.CurrentSkin, false);
			}

			//if (_lastSkin != null)
			//	_lastSkin.Undo.Clear();

			Skin skin = (Skin)treeView1.SelectedNode;
			SetCanSave(skin.Dirty);

			if (skin == null)
			{
				_currentUndoBuffer = null;
				TextureGL.Unbind();

				ColorGrabber currentSkin = new ColorGrabber(GlobalDirtiness.CurrentSkin, 64, 32);
				currentSkin.Save();

				undoToolStripMenuItem.Enabled = undoToolStripButton.Enabled = false;
				redoToolStripMenuItem.Enabled = redoToolStripButton.Enabled = false;
			}
			else
			{
				ColorGrabber glImage = new ColorGrabber(skin.GLImage, skin.Width, skin.Height);
				glImage.Load();

				glImage.Texture = GlobalDirtiness.CurrentSkin;
				glImage.Save();
				glImage.Texture = _previewPaint;
				glImage.Save();

				_currentUndoBuffer = skin.Undo;
				CheckUndo();
			}

			_lastSkin = (Skin)treeView1.SelectedNode;

			SetModel(skin.Model);
			rendererControl.Invalidate();
		}

		void uploadButton_Click(object sender, EventArgs e)
		{
			PerformUpload();
		}

		void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		void animateToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleAnimation();
		}

		void followCursorToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleFollowCursor();
		}

		void grassToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleGrass();
		}

		void addNewSkinToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformImportSkin();
		}

		void deleteSelectedSkinToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformDeleteSkin();
		}

		void cloneSkinToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformCloneSkin();
		}

		private void treeView1_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			//PerformNameChange();
		}

		TreeNode _rightClickedNode = null;
		private void treeView1_MouseUp(object sender, MouseEventArgs e)
		{
			treeView1_MouseDown(sender, e);
			if (e.Button == MouseButtons.Right)
				contextMenuStrip1.Show(Cursor.Position);
		}

		private void treeView1_MouseDown(object sender, MouseEventArgs e)
		{
			_rightClickedNode = treeView1.GetSelectedNodeAt(e.Location);
			changeNameToolStripMenuItem.Enabled = deleteToolStripMenuItem.Enabled = cloneToolStripMenuItem.Enabled = cloneToolStripButton.Enabled = true;
			mDECRESToolStripMenuItem.Enabled = mINCRESToolStripMenuItem.Enabled = true;

			if (treeView1.SelectedNode == null)
			{
				mDECRESToolStripMenuItem.Enabled =
					mINCRESToolStripMenuItem.Enabled =
					changeNameToolStripMenuItem.Enabled =
					deleteToolStripMenuItem.Enabled =
					cloneToolStripMenuItem.Enabled =
					cloneToolStripButton.Enabled = false;
			}
			else if (!(treeView1.SelectedNode is Skin))
			{
				mDECRESToolStripMenuItem.Enabled =
					mINCRESToolStripMenuItem.Enabled =
					cloneToolStripMenuItem.Enabled =
					cloneToolStripButton.Enabled = false;
			}
			else if (treeView1.SelectedNode is Skin)
			{
				var skin = treeView1.SelectedNode as Skin;

				if (skin.Width == 8 || skin.Height == 4)
					mDECRESToolStripMenuItem.Enabled = false;
				//else if (skin.Width == 256 || skin.Height == 128)
				//	mINCRESToolStripMenuItem.Enabled = false;
			}
		}

		void undoToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			PerformUndo();
		}

		void redoToolStripButton_Click(object sender, EventArgs e)
		{
			PerformRedo();
		}

		void redNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromRGBA((byte)redNumericUpDown.Value, SelectedColor.RGB.G, SelectedColor.RGB.B, SelectedColor.RGB.A));
		}

		void greenNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromRGBA(SelectedColor.RGB.R, (byte)greenNumericUpDown.Value, SelectedColor.RGB.B, SelectedColor.RGB.A));
		}

		void blueNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromRGBA(SelectedColor.RGB.R, SelectedColor.RGB.G, (byte)blueNumericUpDown.Value, SelectedColor.RGB.A));
		}

		void alphaNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromRGBA(SelectedColor.RGB.R, SelectedColor.RGB.G, SelectedColor.RGB.B, (byte)alphaNumericUpDown.Value));
		}

		const float oneDivTwoFourty = 1.0f / 240.0f;

		void colorSquare_HueChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL(colorSquare.CurrentHue, (float)colorSquare.CurrentSat * oneDivTwoFourty, (float)saturationSlider.CurrentLum * oneDivTwoFourty);
			//SetColor(Devcorp.Controls.Design.ColorSpaceHelper.HSLtoColor(c));
		}

		void colorSquare_SatChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL(colorSquare.CurrentHue, (float)colorSquare.CurrentSat * oneDivTwoFourty, (float)saturationSlider.CurrentLum * oneDivTwoFourty);
			//SetColor(Devcorp.Controls.Design.ColorSpaceHelper.HSLtoColor(c));
		}

		void saturationSlider_LumChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL(colorSquare.CurrentHue, (float)colorSquare.CurrentSat * oneDivTwoFourty, (float)saturationSlider.CurrentLum * oneDivTwoFourty);
			//SetColor(Devcorp.Controls.Design.ColorSpaceHelper.HSLtoColor(c));
		}

		void hueColorSlider_Scroll(object sender, ScrollEventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL(e.NewValue, (float)saturationColorSlider.Value * oneDivTwoFourty, (float)lightnessColorSlider.Value * oneDivTwoFourty);
			SetColor(ColorManager.FromHSVA(e.NewValue, SelectedColor.HSV.S, SelectedColor.HSV.V, SelectedColor.HSV.A));
		}

		void saturationColorSlider_Scroll(object sender, ScrollEventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL(hueColorSlider.Value, (float)e.NewValue * oneDivTwoFourty, (float)lightnessColorSlider.Value * oneDivTwoFourty);
			SetColor(ColorManager.FromHSVA(SelectedColor.HSV.H, (byte)e.NewValue, SelectedColor.HSV.V, SelectedColor.HSV.A));
		}

		void lightnessColorSlider_Scroll(object sender, ScrollEventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL(hueColorSlider.Value, (float)saturationColorSlider.Value * oneDivTwoFourty, (float)e.NewValue * oneDivTwoFourty);
			SetColor(ColorManager.FromHSVA(SelectedColor.HSV.H, SelectedColor.HSV.S, (byte)e.NewValue, SelectedColor.HSV.A));
		}

		void hueNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL((double)hueNumericUpDown.Value, (float)saturationNumericUpDown.Value * oneDivTwoFourty, (float)luminanceNumericUpDown.Value * oneDivTwoFourty);
			SetColor(ColorManager.FromHSVA((short)hueNumericUpDown.Value, SelectedColor.HSV.S, SelectedColor.HSV.V, SelectedColor.HSV.A));
		}

		void saturationNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL((double)hueNumericUpDown.Value, (float)saturationNumericUpDown.Value * oneDivTwoFourty, (float)luminanceNumericUpDown.Value * oneDivTwoFourty);
			SetColor(ColorManager.FromHSVA(SelectedColor.HSV.H, (byte)saturationNumericUpDown.Value, SelectedColor.HSV.V, SelectedColor.HSV.A));
		}

		void luminanceNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			//var c = new HSL((double)hueNumericUpDown.Value, (float)saturationNumericUpDown.Value * oneDivTwoFourty, (float)luminanceNumericUpDown.Value * oneDivTwoFourty);
			SetColor(ColorManager.FromHSVA(SelectedColor.HSV.H, SelectedColor.HSV.S, (byte)valueNumericUpDown.Value, SelectedColor.HSV.A));
		}


		void redColorSlider_Scroll(object sender, ScrollEventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromRGBA((byte)e.NewValue, SelectedColor.RGB.G, SelectedColor.RGB.B, SelectedColor.RGB.A));
		}

		void greenColorSlider_Scroll(object sender, ScrollEventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromRGBA(SelectedColor.RGB.R, (byte)e.NewValue, SelectedColor.RGB.B, SelectedColor.RGB.A));
		}

		void blueColorSlider_Scroll(object sender, ScrollEventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromRGBA(SelectedColor.RGB.R, SelectedColor.RGB.G, (byte)e.NewValue, SelectedColor.RGB.A));
		}

		void swatchContainer_SwatchChanged(object sender, SwatchChangedEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
				SelectedColor = ColorManager.FromRGBA(e.Swatch.R, e.Swatch.G, e.Swatch.B, e.Swatch.A);
			else
				UnselectedColor = ColorManager.FromRGBA(e.Swatch.R, e.Swatch.G, e.Swatch.B, e.Swatch.A);
		}

		void alphaColorSlider_Scroll(object sender, ScrollEventArgs e)
		{
			if (_skipColors)
				return;

			SetColor(ColorManager.FromHSVA(SelectedColor.HSV.H, SelectedColor.HSV.S, SelectedColor.HSV.V, (byte)e.NewValue));
		}

		bool _editingHex = false;
		private void textBox1_TextChanged(object sender, EventArgs e)
		{
			if (_skipColors)
				return;

			try
			{
				if (textBox1.Text.Contains('#'))
					textBox1.Text = textBox1.Text.Replace("#", "");

				if (textBox1.Text.Length > 8)
					textBox1.Text = textBox1.Text.Remove(8);

				string realHex = textBox1.Text;

				while (realHex.Length != 8)
					realHex += 'F';

				byte r = byte.Parse(realHex.Substring(0, 2), NumberStyles.HexNumber);
				byte g = byte.Parse(realHex.Substring(2, 2), NumberStyles.HexNumber);
				byte b = byte.Parse(realHex.Substring(4, 2), NumberStyles.HexNumber);
				byte a = byte.Parse(realHex.Substring(6, 2), NumberStyles.HexNumber);

				_editingHex = true;
				SetColor(ColorManager.FromRGBA(r, g, b, a));
				_editingHex = false;
			}
			catch
			{
			}
		}

		void perspectiveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Perspective);
		}

		void textureToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Orthographic);
		}

		void perspectiveToolStripButton_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Perspective);
		}

		void orthographicToolStripButton_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Orthographic);
		}

		private void hybridToolStripButton_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Hybrid);
		}

		private void hybridViewToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Hybrid);
		}

		void offToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetTransparencyMode(TransparencyMode.Off);
		}

		void helmetOnlyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetTransparencyMode(TransparencyMode.Helmet);
		}

		void allToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetTransparencyMode(TransparencyMode.All);
		}

		void headToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.HeadFlag);
		}

		void helmetToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.HelmetFlag);
		}

		void chestToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.ChestFlag);
		}

		void leftArmToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.LeftArmFlag);
		}

		void rightArmToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.RightArmFlag);
		}

		void leftLegToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.LeftLegFlag);
		}

		void rightLegToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.RightLegFlag);
		}

		void alphaCheckerboardToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleAlphaCheckerboard();
		}

		void textureOverlayToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleOverlay();
		}

		void ShowUpdater()
		{
			MessageBox.Show("Not finished!");

			Hide();
			
			using (var upd = new MCSkin3D.UpdateSystem.Updater("http://alteredsoftworks.com/mcskin3d/updates.xml", "__installedupdates"))
			{
				if (upd.ShowDialog() == DialogResult.Cancel)
					Show();
				else
					Close();
			}
		}

		void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_updater.Checking)
			{
				_updater.Abort();
				ShowUpdater();
				return;
			}

			//_updater.PrintOnEqual = true;
			//_updater.CheckForUpdate();
			ShowUpdater();
		}

		void undoToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformUndo();
		}

		void redoToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformRedo();
		}
		void keyboardShortcutsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			_shortcutEditor.ShowDialog();
		}

		void backgroundColorToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (MultiPainter.ColorPicker picker = new MultiPainter.ColorPicker())
			{
				picker.CurrentColor = GlobalSettings.BackgroundColor;

				if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					GlobalSettings.BackgroundColor = picker.CurrentColor;

					rendererControl.Invalidate();
				}
			}
		}

		void screenshotToolStripButton_Click(object sender, EventArgs e)
		{
			if ((ModifierKeys & Keys.Shift) != 0)
				SaveScreenshot();
			else
				TakeScreenshot();
		}

		void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformSaveAs();
		}

		void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformSave();
		}

		void saveAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformSaveAll();
		}

		void saveToolStripButton_Click(object sender, EventArgs e)
		{
			PerformSave();
		}

		void saveAlltoolStripButton_Click(object sender, EventArgs e)
		{
			PerformSaveAll();
		}

		void changeNameToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformNameChange();
		}

		void deleteToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformDeleteSkin();
		}

		void cloneToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformCloneSkin();
		}

		void colorTabControl_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (colorTabControl.SelectedIndex == 0 || colorTabControl.SelectedIndex == 1)
			{
				var panel = (Panel)colorTabControl.SelectedTab.Controls[0];

				panel.Controls.Add(colorPick1);
				panel.Controls.Add(colorPreview1);
				panel.Controls.Add(colorPreview2);
				panel.Controls.Add(label5);
				panel.Controls.Add(alphaColorSlider);
				panel.Controls.Add(alphaNumericUpDown);

				if (_secondaryIsFront)
				{
					colorPreview2.BringToFront();
					colorPreview1.SendToBack();
				}
				else
				{
					colorPreview2.SendToBack();
					colorPreview1.BringToFront();
				}
			}
		}

		void automaticallyCheckForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GlobalSettings.AutoUpdate = automaticallyCheckForUpdatesToolStripMenuItem.Checked = !automaticallyCheckForUpdatesToolStripMenuItem.Checked;
		}

		private void toggleHeadToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.HeadFlag);
		}

		private void toggleHelmetToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.HelmetFlag);
		}

		private void toggleChestToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.ChestFlag);
		}

		private void toggleLeftArmToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.LeftArmFlag);
		}

		private void toggleRightArmToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.RightArmFlag);
		}

		private void toggleLeftLegToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.LeftLegFlag);
		}

		private void toggleRightLegToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(VisiblePartFlags.RightLegFlag);
		}

		private void labelEditTextBox_Leave(object sender, EventArgs e)
		{
			DoneEditingNode(labelEditTextBox.Text, _currentlyEditing);
		}

		private void labelEditTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Enter)
			{
				treeView1.Focus();
				e.Handled = true;
			}
		}

		private void labelEditTextBox_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
		{
			if (e.KeyChar == '\r' || e.KeyChar == '\n')
				e.Handled = true;
		}

		private void labelEditTextBox_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
				e.Handled = true;
		}

		private void importHereToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformImportSkin();
		}

		private void toolStripMenuItem1_Click(object sender, EventArgs e)
		{
			PerformNewFolder();
		}

		private void ghostHiddenPartsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleGhosting();
		}

		static ToolStripMenuItem[] _antialiasOpts;
		void SetSampleMenuItem(int samples)
		{
			if (_antialiasOpts == null)
				_antialiasOpts = new ToolStripMenuItem[] { xToolStripMenuItem4, xToolStripMenuItem, xToolStripMenuItem1, xToolStripMenuItem2, xToolStripMenuItem3 };

			int index = 0;

			switch (samples)
			{
			case 0:
			default:
				index = 0;
				break;
			case 1:
				index = 1;
				break;
			case 2:
				index = 2;
				break;
			case 4:
				index = 3;
				break;
			case 8:
				index = 4;
				break;
			}

			foreach (var item in _antialiasOpts)
				item.Checked = false;

			GlobalSettings.Multisamples = samples;
			_antialiasOpts[index].Checked = true;
		}

		private void xToolStripMenuItem4_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(0);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void xToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(1);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void xToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(2);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void xToolStripMenuItem2_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(4);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void xToolStripMenuItem3_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(8);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void SetLanguage(string filename)
		{
		}

		private void ReloadLanguage()
		{
		}

		static Language.Language _currentLanguage;
		public static Language.Language CurrentLanguage
		{
			get { return _currentLanguage; }
			set
			{
				if (_currentLanguage != null)
					_currentLanguage.Item.Checked = false;

				_currentLanguage = value;
				GlobalSettings.LanguageFile = _currentLanguage.Culture.Name;
				MainForm.languageProvider1.LanguageChanged(value);
				MainForm.DarkenLightenOptions.languageProvider1.LanguageChanged(value);
				MainForm.PencilOptions.languageProvider1.LanguageChanged(value);
				MainForm.DodgeBurnOptions.languageProvider1.LanguageChanged(value);
				MainForm.FloodFillOptions.languageProvider1.LanguageChanged(value);
				MainForm.swatchContainer.languageProvider1.LanguageChanged(value);
				MainForm.login.languageProvider1.LanguageChanged(value);
				MainForm.NoiseOptions.languageProvider1.LanguageChanged(value);
				MainForm.EraserOptions.languageProvider1.LanguageChanged(value);
				MainForm.StampOptions.languageProvider1.LanguageChanged(value);
				MainForm._importFromSite.languageProvider1.LanguageChanged(value);

				if (MainForm._selectedTool != null)
					MainForm.toolStripStatusLabel1.Text = MainForm._selectedTool.Tool.GetStatusLabelText();

				_currentLanguage.Item.Checked = true;
			}
		}

		public static string GetLanguageString(string id)
		{
			if (!_currentLanguage.StringTable.ContainsKey(id))
			{
#if BETA
				MessageBox.Show("Stringtable string not found: " + id);
#endif

				return id;
			}

			return _currentLanguage.StringTable[id];
		}

		private void MCSkin3D_Load(object sender, EventArgs e)
		{
		}

		void languageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			CurrentLanguage = (Language.Language)((ToolStripMenuItem)sender).Tag;
		}

		bool _secondaryIsFront = false;

		private void colorPreview1_Click(object sender, EventArgs e)
		{
			_secondaryIsFront = false;
			colorPreview1.BringToFront();

			SetColor(_primaryColor);
		}

		private void colorPreview2_Click(object sender, EventArgs e)
		{
			_secondaryIsFront = true;
			colorPreview2.BringToFront();

			SetColor(_secondaryColor);
		}

		private void toolStripMenuItem3_Click(object sender, EventArgs e)
		{
			PerformUpload();
		}

		private void importToolStripButton_Click(object sender, EventArgs e)
		{
			PerformImportSkin();
		}

		private void newFolderToolStripButton_Click(object sender, EventArgs e)
		{
			PerformNewFolder();
		}

		private void renameToolStripButton_Click(object sender, EventArgs e)
		{
			PerformNameChange();
		}

		private void deleteToolStripButton_Click(object sender, EventArgs e)
		{
			PerformDeleteSkin();
		}

		private void cloneToolStripButton_Click(object sender, EventArgs e)
		{
			PerformCloneSkin();
		}

		private void uploadToolStripButton_Click(object sender, EventArgs e)
		{
			PerformUpload();
		}

		private void toolStripButton2_Click(object sender, EventArgs e)
		{
			PerformTreeViewZoomOut();
		}

		private void toolStripButton1_Click(object sender, EventArgs e)
		{
			PerformTreeViewZoomIn();
		}

		private void colorTabControl_Resize(object sender, EventArgs e)
		{
		}

		private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
		{

		}

		private void hScrollBar1_Scroll(object sender, ScrollEventArgs e)
		{
			treeView1.ScrollPosition = new Point(hScrollBar1.Value, treeView1.ScrollPosition.Y);
		}

		bool ShowDontAskAgain()
		{
			bool againValue = GlobalSettings.ResChangeDontShowAgain;
			bool ret = DontAskAgain.Show(CurrentLanguage, "M_IRREVERSIBLE", ref againValue);
			GlobalSettings.ResChangeDontShowAgain = againValue;

			return ret;
		}

		void PerformDecreaseResolution()
		{
			if (_lastSkin == null)
				return;
			if (_lastSkin.Width == 8 || _lastSkin.Height == 4)
				return;
			if (!ShowDontAskAgain())
				return;

			_lastSkin.Resize(_lastSkin.Width / 2, _lastSkin.Height / 2);

			ColorGrabber grabber = new ColorGrabber(_lastSkin.GLImage, _lastSkin.Width, _lastSkin.Height);
			grabber.Load();
			grabber.Texture = GlobalDirtiness.CurrentSkin;
			grabber.Save();
			grabber.Texture = _previewPaint;
			grabber.Save();
		}

		void PerformIncreaseResolution()
		{
			if (_lastSkin == null)
				return;
			//if (_lastSkin.Width == 256 || _lastSkin.Height == 128)
			//	return;
			if (!ShowDontAskAgain())
				return;

			_lastSkin.Resize(_lastSkin.Width * 2, _lastSkin.Height * 2);

			ColorGrabber grabber = new ColorGrabber(_lastSkin.GLImage, _lastSkin.Width, _lastSkin.Height);
			grabber.Load();
			grabber.Texture = GlobalDirtiness.CurrentSkin;
			grabber.Save();
			grabber.Texture = _previewPaint;
			grabber.Save();
		}

		ImportSite _importFromSite = new ImportSite();
		public void PerformImportFromSite()
		{
			string accountName = _importFromSite.Show();

			if (string.IsNullOrEmpty(accountName))
				return;

			var url = "http://s3.amazonaws.com/MinecraftSkins/" + accountName + ".png";

			string folderLocation;
			TreeNodeCollection collection;

			if (_rightClickedNode == null)
				_rightClickedNode = treeView1.SelectedNode;

			GetFolderLocationAndCollectionForNode(treeView1, _rightClickedNode, out folderLocation, out collection);

			string newSkinName = accountName;

			while (File.Exists(folderLocation + newSkinName + ".png"))
				newSkinName += " - New";

			try
			{
				byte[] pngData = Paril.Net.WebHelpers.DownloadFile(url);

				using (var file = File.Create(folderLocation + newSkinName + ".png"))
					file.Write(pngData, 0, pngData.Length);

				var skin = new Skin(folderLocation + newSkinName + ".png");
				collection.Add(skin);
				skin.SetImages();

				treeView1.Invalidate();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, GetLanguageString("M_SKINERROR") + "\r\n" + ex.ToString());
				return;
			}
		}

		private void mDECRESToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformDecreaseResolution();
		}

		private void mINCRESToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformIncreaseResolution();
		}

		private void toolStripButton4_Click(object sender, EventArgs e)
		{
			PerformDecreaseResolution();
		}

		private void toolStripButton3_Click(object sender, EventArgs e)
		{
			PerformIncreaseResolution();
		}

		private void toolStripButton5_Click(object sender, EventArgs e)
		{
			PerformNewSkin();
		}

		private void toolStripButton6_Click(object sender, EventArgs e)
		{
			PerformImportFromSite();
		}

		private void mFETCHNAMEToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformImportFromSite();
		}

		private void toolStripMenuItem4_Click(object sender, EventArgs e)
		{
			PerformNewSkin();
		}

		private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{

		}

		bool _waitingForRestart = false;
		string[] _newSkinDirs = null;

		private void mSKINDIRSToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_waitingForRestart)
			{
				MessageBox.Show(GetLanguageString("C_RESTART"), GetLanguageString("C_RESTART_CAPTION"), MessageBoxButtons.OK);
				return;
			}

			using (DirectoryList dl = new DirectoryList())
			{
				dl.StartPosition = FormStartPosition.CenterParent;
				foreach (var dir in GlobalSettings.SkinDirectories.OrderBy(x => x))
					dl.Directories.Add(dir);

				if (dl.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					if (RecursiveNodeIsDirty(treeView1.Nodes))
					{
						var mb = MessageBox.Show(GetLanguageString("D_UNSAVED"), GetLanguageString("D_UNSAVED_CAPTION"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
						switch (mb)
						{
						case System.Windows.Forms.DialogResult.No:
							_waitingForRestart = true;
							_newSkinDirs = dl.Directories.ToArray();
							return;
						case System.Windows.Forms.DialogResult.Cancel:
							return;
						}
					}

					GlobalSettings.SkinDirectories = dl.Directories.ToArray();
				}
			}
		}

		private void modeToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			Perform10Mode();
		}

		private void mINVERTBOTTOMToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (treeView1.SelectedNode == null ||
				_lastSkin == null)
				return;

			ColorGrabber grabber = new ColorGrabber(GlobalDirtiness.CurrentSkin, _lastSkin.Width, _lastSkin.Height);
			grabber.Load();

			List<Rectangle> toInvert = new List<Rectangle>();

			foreach (var meshes in CurrentModel.Meshes)
			{
				foreach (var face in meshes.Faces)
				{
					if (face.Downface)
					{
						var rect = face.TexCoordsToInteger(_lastSkin.Width, _lastSkin.Height);

						if (!toInvert.Contains(rect))
							toInvert.Add(rect);
					}
				}
			}

			PixelsChangedUndoable undoable = new PixelsChangedUndoable(Editor.GetLanguageString("U_PIXELSCHANGED"), Editor.GetLanguageString("M_INVERTBOTTOM"));

			foreach (var rect in toInvert)
			{
				for (int x = rect.X; x < rect.X + rect.Width; ++x)
				{
					for (int y = rect.Y, y2 = rect.Y + rect.Height - 1; y2 > y; ++y, --y2)
					{
						var topPixel = grabber[x, y];
						var bottomPixel = grabber[x, y2];

						undoable.Points.Add(new Point(x, y), Tuple.MakeTuple(Color.FromArgb(topPixel.Alpha, topPixel.Red, topPixel.Green, topPixel.Blue), new ColorAlpha(Color.FromArgb(bottomPixel.Alpha, bottomPixel.Red, bottomPixel.Green, bottomPixel.Blue), -1)));
						undoable.Points.Add(new Point(x, y2), Tuple.MakeTuple(Color.FromArgb(bottomPixel.Alpha, bottomPixel.Red, bottomPixel.Green, bottomPixel.Blue), new ColorAlpha(Color.FromArgb(topPixel.Alpha, topPixel.Red, topPixel.Green, topPixel.Blue), -1)));

						grabber[x, y] = bottomPixel;
						grabber[x, y2] = topPixel;
					}
				}
			}

			_lastSkin.Undo.AddBuffer(undoable);
			CheckUndo();
			SetCanSave(_lastSkin.Dirty = true);

			grabber.Save();
		}

		void mLINESIZEToolStripMenuItem_NumericBox_ValueChanged(object sender, EventArgs e)
		{
			GlobalSettings.DynamicOverlayLineSize = (int)mLINESIZEToolStripMenuItem.NumericBox.Value;

			//SetModel(CurrentModel);
		}

		void mOVERLAYTEXTSIZEToolStripMenuItem_NumericBox_ValueChanged(object sender, EventArgs e)
		{
			GlobalSettings.DynamicOverlayTextSize = (int)mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.Value;

			//SetModel(CurrentModel);
		}

		ModelToolStripMenuItem _oldModel = null;
		BackgroundImage _dynamicOverlay = null;
		public void SetModel(Model Model)
		{
			if (_lastSkin == null)
				return;

			//if (_dynamicOverlay.GLImage != null)
			//	_dynamicOverlay.GLImage.Dispose();

			//_dynamicOverlay.GLImage = new TextureGL(Model.GenerateOverlay(GlobalSettings.DynamicOverlayLineColor, GlobalSettings.DynamicOverlayTextColor, Model.AspectRatio, 1 << GlobalSettings.DynamicOverlayTextSize, GlobalSettings.DynamicOverlayLineSize));
			//_dynamicOverlay.GLImage.SetMipmapping(false);
			//_dynamicOverlay.GLImage.SetRepeat(false);

			if (_oldModel != null &&
				_oldModel.Model == Model)
				return;

			if (_lastSkin.Model != Model)
			{
				_lastSkin.Model = Model;

				_lastSkin.Dirty = true;
				SetCanSave(true);
				CheckUndo();
			}

			if (_oldModel != null)
			{
				_oldModel.Checked = false;

				for (var parent = _oldModel.OwnerItem; parent != null; parent = parent.OwnerItem)
					parent.Image = null;
			}

			toolStripDropDownButton1.Text = _lastSkin.Model.Name;
			_oldModel = _lastSkin.Model.DropDownItem;
			_oldModel.Checked = true;

			for (var parent = _oldModel.OwnerItem; parent != null; parent = parent.OwnerItem)
				parent.Image = Properties.Resources.right_arrow_next;
		}

		public static Vector3 CameraPosition
		{
			get;
			private set;
		}

		public static float GrassY
		{
			get;
			private set;
		}

		private void resetCameraToolStripButton_Click(object sender, EventArgs e)
		{
			PerformResetCamera();
		}

		private void mLINECOLORToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (MultiPainter.ColorPicker picker = new MultiPainter.ColorPicker())
			{
				picker.CurrentColor = GlobalSettings.DynamicOverlayLineColor;

				if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					GlobalSettings.DynamicOverlayLineColor = picker.CurrentColor;

					SetModel(CurrentModel);
					rendererControl.Invalidate();
				}
			}
		}

		private void mTEXTCOLORToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (MultiPainter.ColorPicker picker = new MultiPainter.ColorPicker())
			{
				picker.CurrentColor = GlobalSettings.DynamicOverlayTextColor;

				if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					GlobalSettings.DynamicOverlayTextColor = picker.CurrentColor;

					SetModel(CurrentModel);
					rendererControl.Invalidate();
				}
			}
		}

		private void mINFINITEMOUSEToolStripMenuItem_Click(object sender, EventArgs e)
		{
			mINFINITEMOUSEToolStripMenuItem.Checked = !mINFINITEMOUSEToolStripMenuItem.Checked;
			GlobalSettings.InfiniteMouse = mINFINITEMOUSEToolStripMenuItem.Checked;
		}

		void PerformBrowseTo()
		{
			if (treeView1.SelectedNode is Skin)
				Process.Start("explorer.exe", "/select,\"" + ((Skin)treeView1.SelectedNode).File.FullName + "\"");
			else
				Process.Start("explorer.exe", ((FolderNode)treeView1.SelectedNode).Directory.FullName);
		}

		private void bROWSEIDToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformBrowseTo();
		}

		private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
		{
			bROWSEIDToolStripMenuItem.Text = string.Format(GetLanguageString("M_BROWSE"), (treeView1.SelectedNode is Skin) ? GetLanguageString("M_SKIN") : GetLanguageString("M_FOLDER"));
		}

		private void mRENDERSTATSToolStripMenuItem_Click(object sender, EventArgs e)
		{
			mRENDERSTATSToolStripMenuItem.Checked = !mRENDERSTATSToolStripMenuItem.Checked;
			GlobalSettings.RenderBenchmark = mRENDERSTATSToolStripMenuItem.Checked;
		}

		Form popoutForm = null;
		private void toolStripButton7_Click(object sender, EventArgs e)
		{
			splitContainer1.Panel2Collapsed = !splitContainer1.Panel2Collapsed;

			if (popoutForm == null)
			{
				var oldWidth = Width;
				Width = helpToolStripMenuItem.Bounds.X + helpToolStripMenuItem.Bounds.Width + 18;

				// Move the items to a new form
				popoutForm = new Form();
				popoutForm.Height = Height;
				popoutForm.Width = (oldWidth - Width) + 4;
				popoutForm.Text = "Render Window";
				popoutForm.Icon = Icon;
				popoutForm.ControlBox = false;
				popoutForm.Show();
				popoutForm.Location = new Point(Location.X + Width, Location.Y);

				splitContainer1.Panel2.Controls.Remove(panel4);
				popoutForm.Controls.Add(panel4);
			}
			else
			{
				popoutForm.Controls.Remove(panel4);
				splitContainer1.Panel2.Controls.Add(panel4);

				Width += popoutForm.Width - 4;

				popoutForm.Close();
				popoutForm.Dispose();
				popoutForm = null;
			}
		}

		public static string RootFolderString
		{
			get
			{
				if (HasOneRoot)
					return Path.GetDirectoryName(GlobalSettings.SkinDirectories[0]);

				throw new InvalidOperationException();
			}
		}

		public static bool HasOneRoot
		{
			get { return GlobalSettings.SkinDirectories.Length == 1; }
		}
	}
}