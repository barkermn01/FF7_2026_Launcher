#define TRACE
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FF7_Launcher.Properties;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using SharpDX.XInput;

namespace FF7_Launcher;

public class FF7Launcher : Form
{
	public delegate int RunGamePROC();

	public enum ControlType
	{
		PlayButton,
		SettingButton,
		ExitButton,
		ControlNum
	}

	private enum ImgType
	{
		Deactivate,
		MouseOn,
		Selected
	}

	public static string languageInstalled = "jp";

	public static FF7ConfigFile configFile = new FF7ConfigFile();

	private SteamManager SteamMan;

	public InputManager inputManager = new InputManager();

	public DXInputManager DXInput;

	public Form currentForm;

	public int controlIndex = 0;

	private DInput dinput = null;

	private bool bCancelButtonDown = false;

	private CustomVisualButton CustomPlayButton;

	private CustomVisualButton CustomSettingButton;

	private CustomVisualButton CustomExitButton;

	private List<CustomVisualButton> ButtonList = new List<CustomVisualButton>();

	private Graphics Renderer;

	private Bitmap BTN_Play_00_Deactive = null;

	private Bitmap BTN_Play_01_MouseOn = null;

	private Bitmap BTN_Play_02_Selected = null;

	private Bitmap BTN_Setting_00_Deactive = null;

	private Bitmap BTN_Setting_01_MouseOn = null;

	private Bitmap BTN_Setting_02_Selected = null;

	private Bitmap BTN_Exit_00_Deactive = null;

	private Bitmap BTN_Exit_01_MouseOn = null;

	private Bitmap BTN_Exit_02_Selected = null;

	private Point prevMousePos = new Point(-1, -1);

	private Image CacheBackGround;

	private Rectangle ImageSizeBound;

	private Rectangle OriginalBound;

	private SoundManager Sound;

	private bool KeyPressing = false;

	private float Ratio = 1f;

	private bool NeedRedraw = false;

	private bool ReadInput = true;

	private volatile bool Running = false;

	private SharpDX.Direct3D11.Device device;

	private SwapChain swapChain;

	private Thread RenderThread;

	private bool IsFocusedByJoypad = false;

	private bool IsShowing = true;

	private bool CanPressPlay = true;

	private bool _SettingFormOpened = false;

	private IContainer components = null;

	private CheckBox chkLaunch7thHeaven;

	private CheckBox chkBypassLauncher;

	private Button playBtn;

	private Button settingBtn;

	private System.Windows.Forms.Timer timer1;

	private Button ExitButton;

	public static bool IsSmall => Screen.PrimaryScreen.WorkingArea.Height < 800;

	public bool IsActive => Form.ActiveForm == this;

	private bool SettingFormOpened
	{
		get
		{
			return _SettingFormOpened;
		}
		set
		{
			SettingButtonPress(value);
			_SettingFormOpened = value;
		}
	}

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern IntPtr LoadLibrary(string lpFileName);

	[DllImport("kernel32", SetLastError = true)]
	internal static extern bool FreeLibrary(IntPtr hModule);

	[DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
	internal static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

	public FF7Launcher()
	{
		InitializeComponent();
		Sound = new SoundManager(this);
		base.KeyPreview = true;
		base.KeyDown += FF7Launcher_KeyDown;
		base.KeyUp += FF7Launcher_KeyUp;
		int captionHeight = SystemInformation.CaptionHeight;
		System.Drawing.Size fixedFrameBorderSize = SystemInformation.FixedFrameBorderSize;
		System.Drawing.Size size = new System.Drawing.Size(BackgroundImage.Width, BackgroundImage.Height);
		Trace.WriteLine("Initial Bound : " + base.Bounds.ToString());
		System.Drawing.Size size2 = size;
		Trace.WriteLine("originalSize : " + size2.ToString());
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.DoubleBuffer, value: true);
		SetStyle(ControlStyles.UserPaint, value: true);
		SteamMan = new SteamManager();
		SteamMan.Init();
		configFile.SteamID = SteamMan.SteamID;
		CacheBackGround = BackgroundImage;
		BackgroundImage = null;
		if (!configFile.Read("config.txt"))
		{
		}
		languageInstalled = configFile.m_language.ToLower();
		string text;
		if (languageInstalled == "jp")
		{
			text = "ja";
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja", useUserOverride: false);
		}
		else if (languageInstalled == "en")
		{
			text = "en";
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("en", useUserOverride: false);
		}
		else if (languageInstalled == "fr")
		{
			text = "fr";
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr", useUserOverride: false);
		}
		else if (languageInstalled == "de")
		{
			text = "de";
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("de", useUserOverride: false);
		}
		else if (languageInstalled == "es")
		{
			text = "es";
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("es", useUserOverride: false);
		}
		else
		{
			text = Thread.CurrentThread.CurrentUICulture.Name.Substring(0, 2).ToLower();
			if (text != "ja" && text != "en" && text != "fr" && text != "de" && text != "es")
			{
				text = "en";
			}
			Thread.CurrentThread.CurrentUICulture = new CultureInfo(text, useUserOverride: false);
		}
		inputManager.Init();
		playBtn.Hide();
		settingBtn.Hide();
		ExitButton.Hide();
		Renderer = CreateGraphics();
		CustomPlayButton = new CustomVisualButton(new Point(506, 620), Control.MousePosition, ButtonType.Play);
		CustomPlayButton.OnClick += CustomPlayButton_OnClickCommitted;
		CustomPlayButton.OnNeedRepaint += OnNeedRepaint;
		CustomPlayButton.OnMouseHover += CustomButton_OnMouseHover;
		ButtonList.Add(CustomPlayButton);
		CustomSettingButton = new CustomVisualButton(new Point(763, 657), Control.MousePosition, ButtonType.Setting);
		CustomSettingButton.OnClick += CustomSettingButton_OnClickCommitted;
		CustomSettingButton.OnNeedRepaint += OnNeedRepaint;
		CustomSettingButton.OnMouseHover += CustomButton_OnMouseHover;
		ButtonList.Add(CustomSettingButton);
		CustomExitButton = new CustomVisualButton(new Point(1020, 657), Control.MousePosition, ButtonType.Exit);
		CustomExitButton.OnClick += CustomExitButton_OnClick;
		CustomExitButton.OnNeedRepaint += OnNeedRepaint;
		CustomExitButton.OnMouseHover += CustomButton_OnMouseHover;
		ButtonList.Add(CustomExitButton);
		InitializeCheckboxes();
		base.MouseDown += FF7Launcher_MouseDown;
		ImageSizeBound = base.Bounds;
		OriginalBound = base.Bounds;
		ImageSizeBound.Size = size;
		Rectangle imageSizeBound = ImageSizeBound;
		Trace.WriteLine("ImageSizeBound : " + imageSizeBound.ToString());
		RuntimeLocalizer.OnChangeCulture += OnChangeCulture;
		RuntimeLocalizer.ChangeCulture(this, text);
		timer1.Interval = 10;
		timer1.Start();
		dinput = new DInput();
		if (inputManager.IsConnected())
		{
			StartXInputLoop();
		}
		else if (DInput.GetGamePadNum() > 0 && dinput.CreateDInputDevice(this))
		{
			dinput.Acquire();
			StartDInputLoop();
		}
		DXInput = new DXInputManager(dinput, inputManager);
		NativeMethods.RegisterUsbDeviceNotification(base.Handle);
		currentForm = this;
		playBtn.Text = "";
		settingBtn.Text = "";
		ExitButton.Text = "";
		playBtn.BackgroundImage = BTN_Play_00_Deactive;
		settingBtn.BackgroundImage = BTN_Setting_00_Deactive;
		ExitButton.BackgroundImage = BTN_Exit_00_Deactive;
		FocusOnlyOne(controlIndex);
		Invalidate();
	}

	private void InitializeCheckboxes()
	{
		chkBypassLauncher = new CheckBox
		{
			Text = "Bypass Launcher",
			Location = new Point(763, 620),
			AutoSize = true,
			ForeColor = Color.White,
			BackColor = Color.Transparent,
			Checked = configFile.BypassLauncher == 1
		};
		chkBypassLauncher.CheckedChanged += (s, e) =>
		{
			configFile.BypassLauncher = chkBypassLauncher.Checked ? 1 : 0;
			configFile.Write("config.txt");
		};
		Controls.Add(chkBypassLauncher);
		string seventhHeavenPath = Get7thHeavenPath();
		if (seventhHeavenPath != null)
		{
			chkLaunch7thHeaven = new CheckBox
			{
				Text = "Launch 7th Heaven",
				Location = new Point(763, 640),
				AutoSize = true,
				ForeColor = Color.White,
				BackColor = Color.Transparent,
				Checked = configFile.Launch7thHeaven == 1
			};
			chkLaunch7thHeaven.CheckedChanged += (s, e) =>
			{
				configFile.Launch7thHeaven = chkLaunch7thHeaven.Checked ? 1 : 0;
				configFile.Write("config.txt");
			};
			Controls.Add(chkLaunch7thHeaven);
		}
	}

	private static string Get7thHeavenPath()
	{
		try
		{
			using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{E66AE545-C285-4B8C-8BD0-67282E160BF4}_is1"))
			{
				if (key != null)
				{
					string installLocation = key.GetValue("InstallLocation") as string;
					if (!string.IsNullOrEmpty(installLocation))
					{
						string exePath = Path.Combine(installLocation, "7th Heaven.exe");
						if (File.Exists(exePath))
						{
							return exePath;
						}
					}
				}
			}
		}
		catch { }
		return File.Exists("7thHeaven.exe") ? "7thHeaven.exe" : null;
	}

	private void CustomButton_OnMouseHover(object sender, EventArgs e)
	{
		Sound.PlayInstance(SoundManager.SoundType.Cursor);
	}

	private void OnNeedRepaint(object sender, EventArgs e)
	{
		Invalidate();
	}

	private void FF7Launcher_KeyUp(object sender, KeyEventArgs e)
	{
		KeyPressing = false;
	}

	private void FF7Launcher_KeyDown(object sender, KeyEventArgs e)
	{
		if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.A) && !KeyPressing)
		{
			AddControlIndex(-1);
			Sound.PlayInstance(SoundManager.SoundType.Cursor);
			FocusOnlyOne(controlIndex);
			KeyPressing = true;
		}
		if ((e.KeyCode == Keys.Right || e.KeyCode == Keys.D) && !KeyPressing)
		{
			AddControlIndex(1);
			Sound.PlayInstance(SoundManager.SoundType.Cursor);
			FocusOnlyOne(controlIndex);
			KeyPressing = true;
		}
		if (e.KeyCode == Keys.Return || e.KeyCode == Keys.X)
		{
			CommitButton();
			KeyPressing = true;
		}
	}

	protected override void WndProc(ref System.Windows.Forms.Message m)
	{
		base.WndProc(ref m);
		if (m.Msg == 537)
		{
			switch ((int)m.WParam)
			{
			case 32772:
				Usb_DeviceAdded();
				break;
			case 32768:
				Usb_DeviceAdded();
				break;
			}
		}
	}

	private void Usb_DeviceAdded()
	{
		if (dinput != null)
		{
			dinput.OnTick(null);
		}
		if (inputManager != null)
		{
			inputManager.OnTick(null);
		}
	}

	private float ResizeAndCentering()
	{
		if (IsSmall)
		{
			Trace.WriteLine(IsSmall);
			NeedRedraw = true;
			Ratio = 0.5f;
		}
		ResizeIfNecessary();
		return CenteringWindow();
	}

	private void ResizeIfNecessary()
	{
		if (NeedRedraw)
		{
			int captionHeight = SystemInformation.CaptionHeight;
			System.Drawing.Size fixedFrameBorderSize = SystemInformation.FixedFrameBorderSize;
			Rectangle rectangle = ImageSizeBound.Resize(Ratio);
			Trace.WriteLine("Ratio : " + Ratio);
			SetBounds(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
		}
	}

	private void ArrangeButton(float ratio)
	{
		foreach (CustomVisualButton button in ButtonList)
		{
			button.Resize(ratio);
		}
	}

	private Rectangle SetLTRB(float l, float t, float r, float b)
	{
		return new Rectangle((int)l, (int)t, (int)(r - l), (int)(b - t));
	}

	private float CenteringWindow()
	{
		Rectangle r = ImageSizeBound.Resize(Ratio);
		Rectangle param = RectangleToScreen(r);
		int newWidth = r.Width;
		int newHeight = r.Height;
		NativeMethods.AdjustWindowRect(base.Handle, ref param, newWidth, newHeight, hasVScroll: false);
		Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
		Point location = new Point(workingArea.Width / 2 - param.Width / 2, workingArea.Height / 2 - param.Height / 2);
		base.Width = param.Width;
		base.Height = param.Height;
		base.Location = location;
		Trace.WriteLine("ratio : " + (float)base.Width / (float)OriginalBound.Width);
		int captionHeight = SystemInformation.CaptionHeight;
		System.Drawing.Size fixedFrameBorderSize = SystemInformation.FixedFrameBorderSize;
		Trace.WriteLine("centering " + base.Bounds.ToString());
		return 1f;
	}

	private void ReassignImage()
	{
		List<Bitmap> list = new List<Bitmap>();
		list.Add(BTN_Play_00_Deactive);
		list.Add(BTN_Play_01_MouseOn);
		list.Add(BTN_Play_02_Selected);
		CustomPlayButton.AssignImage(list);
		List<Bitmap> list2 = new List<Bitmap>();
		list2.Add(BTN_Setting_00_Deactive);
		list2.Add(BTN_Setting_01_MouseOn);
		list2.Add(BTN_Setting_02_Selected);
		CustomSettingButton.AssignImage(list2);
		List<Bitmap> list3 = new List<Bitmap>();
		list3.Add(BTN_Exit_00_Deactive);
		list3.Add(BTN_Exit_01_MouseOn);
		list3.Add(BTN_Exit_02_Selected);
		CustomExitButton.AssignImage(list3);
	}

	private void CustomSettingButton_OnClickCommitted(object sender, EventArgs e)
	{
		settingBtn_Click(this, null);
	}

	private void CustomPlayButton_OnClickCommitted(object sender, EventArgs e)
	{
		Sound.PlayInstance(SoundManager.SoundType.Decide);
		playBtn_Click(this, null);
	}

	private void CustomExitButton_OnClick(object sender, EventArgs e)
	{
		Sound.PlayInstance(SoundManager.SoundType.Cancel);
		ExitBtn_Click();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		e.Graphics.DrawImage(CacheBackGround, base.ClientRectangle);
		foreach (CustomVisualButton button in ButtonList)
		{
			button.OnPaint(e);
		}
		NeedRedraw = false;
	}

	private void OnChangeCulture(object sender, ChangeCultureEventArgs e)
	{
		if (sender == this)
		{
			playBtn.Text = "";
			settingBtn.Text = "";
			SetButtonImage(e);
			playBtn.BackgroundImage = BTN_Play_00_Deactive;
			SetImage(ImgType.Selected);
			if (e.CultureType == InternationalCultureType.JP)
			{
				CacheBackGround = Resources.UI_HUD_Launcher_BG;
			}
			else
			{
				CacheBackGround = Resources.UI_HUD_Launcher_BG_EFIGS;
			}
			ResizeAndCentering();
			ArrangeButton(Ratio);
			Invalidate();
		}
	}

	private void SetButtonImage(ChangeCultureEventArgs e)
	{
		switch (e.CultureType)
		{
		case InternationalCultureType.JP:
			BTN_Play_00_Deactive = Resources.UI_HUD_Launcher_BTN_Play_00_JP;
			BTN_Play_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Play_01_JP;
			BTN_Play_02_Selected = Resources.UI_HUD_Launcher_BTN_Play_02_JP;
			BTN_Setting_00_Deactive = Resources.UI_HUD_Launcher_BTN_Setting_00_JP;
			BTN_Setting_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Setting_01_JP;
			BTN_Setting_02_Selected = Resources.UI_HUD_Launcher_BTN_Setting_02_JP;
			BTN_Exit_00_Deactive = Resources.UI_HUD_Launcher_BTN_exit_00_JP;
			BTN_Exit_01_MouseOn = Resources.UI_HUD_Launcher_BTN_exit_01_JP;
			BTN_Exit_02_Selected = Resources.UI_HUD_Launcher_BTN_exit_02_JP;
			break;
		case InternationalCultureType.EN:
			BTN_Play_00_Deactive = Resources.UI_HUD_Launcher_BTN_Play_00_EN;
			BTN_Play_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Play_01_EN;
			BTN_Play_02_Selected = Resources.UI_HUD_Launcher_BTN_Play_02_EN;
			BTN_Setting_00_Deactive = Resources.UI_HUD_Launcher_BTN_Setting_00_EN;
			BTN_Setting_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Setting_01_EN;
			BTN_Setting_02_Selected = Resources.UI_HUD_Launcher_BTN_Setting_02_EN;
			BTN_Exit_00_Deactive = Resources.UI_HUD_Launcher_BTN_exit_00_EN;
			BTN_Exit_01_MouseOn = Resources.UI_HUD_Launcher_BTN_exit_01_EN;
			BTN_Exit_02_Selected = Resources.UI_HUD_Launcher_BTN_exit_02_EN;
			break;
		case InternationalCultureType.FR:
			BTN_Play_00_Deactive = Resources.UI_HUD_Launcher_BTN_play_00_FR;
			BTN_Play_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Play_01_FR;
			BTN_Play_02_Selected = Resources.UI_HUD_Launcher_BTN_Play_02_FR;
			BTN_Setting_00_Deactive = Resources.UI_HUD_Launcher_BTN_Setting_00_FR;
			BTN_Setting_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Setting_01_FR;
			BTN_Setting_02_Selected = Resources.UI_HUD_Launcher_BTN_Setting_02_FR;
			BTN_Exit_00_Deactive = Resources.UI_HUD_Launcher_BTN_exit_00_FR;
			BTN_Exit_01_MouseOn = Resources.UI_HUD_Launcher_BTN_exit_01_FR;
			BTN_Exit_02_Selected = Resources.UI_HUD_Launcher_BTN_exit_02_FR;
			break;
		case InternationalCultureType.DE:
			BTN_Play_00_Deactive = Resources.UI_HUD_Launcher_BTN_play_00_DE;
			BTN_Play_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Play_01_DE;
			BTN_Play_02_Selected = Resources.UI_HUD_Launcher_BTN_Play_02_DE;
			BTN_Setting_00_Deactive = Resources.UI_HUD_Launcher_BTN_Setting_00_DE;
			BTN_Setting_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Setting_01_DE;
			BTN_Setting_02_Selected = Resources.UI_HUD_Launcher_BTN_Setting_02_DE;
			BTN_Exit_00_Deactive = Resources.UI_HUD_Launcher_BTN_exit_00_DE;
			BTN_Exit_01_MouseOn = Resources.UI_HUD_Launcher_BTN_exit_01_DE;
			BTN_Exit_02_Selected = Resources.UI_HUD_Launcher_BTN_exit_02_DE;
			break;
		case InternationalCultureType.ES:
			BTN_Play_00_Deactive = Resources.UI_HUD_Launcher_BTN_play_00_ES;
			BTN_Play_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Play_01_ES;
			BTN_Play_02_Selected = Resources.UI_HUD_Launcher_BTN_Play_02_ES;
			BTN_Setting_00_Deactive = Resources.UI_HUD_Launcher_BTN_Setting_00_ES;
			BTN_Setting_01_MouseOn = Resources.UI_HUD_Launcher_BTN_Setting_01_ES;
			BTN_Setting_02_Selected = Resources.UI_HUD_Launcher_BTN_Setting_02_ES;
			BTN_Exit_00_Deactive = Resources.UI_HUD_Launcher_BTN_exit_00_ES;
			BTN_Exit_01_MouseOn = Resources.UI_HUD_Launcher_BTN_exit_01_ES;
			BTN_Exit_02_Selected = Resources.UI_HUD_Launcher_BTN_exit_02_ES;
			break;
		}
		ReassignImage();
	}

	private void StartXInputLoop()
	{
		Running = true;
		RenderThread = new Thread(StartRenderLoop);
		RenderThread.Start();
	}

	private void StartRenderLoop()
	{
		try
		{
			Form form = new Form();
			form.Hide();
			SwapChainDescription swapChainDescription = new SwapChainDescription
			{
				BufferCount = 2,
				ModeDescription = new ModeDescription(1, 1, new Rational(60, 1), Format.R8G8B8A8_UNorm),
				IsWindowed = true,
				OutputHandle = form.Handle,
				SampleDescription = new SampleDescription(1, 0),
				SwapEffect = SwapEffect.Discard,
				Usage = Usage.RenderTargetOutput
			};
			SharpDX.Direct3D11.Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.None, swapChainDescription, out device, out swapChain);
			RenderLoop renderLoop = new RenderLoop();
			while (Running)
			{
				renderLoop.NextFrame();
				XInputLoop();
				if (swapChain != null)
				{
					swapChain.Present(1, PresentFlags.None);
				}
				Application.DoEvents();
				Thread.Sleep(16);
			}
		}
		catch (Exception)
		{
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(FF7Launcher));
			MessageBox.Show(componentResourceManager.GetString("FailedLaunchError"), Program.Title);
			Environment.Exit(0);
		}
	}

	private void XInputLoop()
	{
		try
		{
			if (ReadInput && IsActive)
			{
				UpdateControlInputs();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString(), Program.Title);
		}
	}

	private void StartDInputLoop()
	{
		Running = true;
		new Thread(DInputLoop).Start();
	}

	private void DInputLoop()
	{
		try
		{
			while (Running)
			{
				if (ReadInput && IsActive)
				{
					UpdateControlDInputs();
				}
				Thread.Sleep(16);
			}
			if (bCancelButtonDown)
			{
				Thread.Sleep(500);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString(), Program.Title);
		}
		Console.WriteLine("DInputLoop end");
	}

	private void UpdateControlDInputs()
	{
		if (dinput != null)
		{
			dinput.Update(0.01666f);
			ApplyDInput();
		}
	}

	private void ApplyDInput()
	{
		if (currentForm != null)
		{
			ApplyDInputLauncher();
			ProcessDXInput();
		}
	}

	private bool ApplyDInputLauncher()
	{
		bool result = false;
		if (dinput.ControllerButtonJustPressed(DInput.DINPUT_BUTTON.DIB_LEFT))
		{
			AddControlIndex(-1);
			ApplyInputEvent();
			result = true;
		}
		else if (dinput.ControllerButtonJustPressed(DInput.DINPUT_BUTTON.DIB_RIGHT))
		{
			AddControlIndex(1);
			ApplyInputEvent();
			result = true;
		}
		return result;
	}

	private void UpdateControlInputs()
	{
		inputManager.Update(0.01666f);
		ApplyInput();
	}

	private void ApplyInput()
	{
		if (currentForm != null)
		{
			ApplyInputLauncher();
			ProcessDXInput();
		}
	}

	private void ProcessDXInput()
	{
		if (DXInput.ControllerButtonJustPressed(JoyKey.Select))
		{
			Sound.PlayInstance(SoundManager.SoundType.Decide);
			Select_CallEventHandlerFormInputThread();
		}
		else if (DXInput.ControllerButtonJustPressed(JoyKey.Cansel))
		{
			Sound.PlayInstance(SoundManager.SoundType.ExitButton);
			bCancelButtonDown = true;
			Thread.Sleep(500);
			Cancel_CallEventHandlerFormInputThread();
		}
	}

	private void AddControlIndex(int delta)
	{
		int num = controlIndex + delta;
		if (num == 3)
		{
			num = 0;
		}
		if (num < 0)
		{
			num = 2;
		}
		if (!IsFocusedByJoypad)
		{
			num = 0;
		}
		controlIndex = num;
	}

	private bool ApplyInputLauncher()
	{
		bool result = false;
		bool flag = false;
		bool flag2 = false;
		if (inputManager.ControllerStickJustPressed(GamepadKeyCode.LeftThumbLeft))
		{
			flag2 = true;
		}
		else if (inputManager.ControllerStickJustPressed(GamepadKeyCode.LeftThumbRight))
		{
			flag = true;
		}
		if (inputManager.ControllerButtonJustPressed(GamepadButtonFlags.DPadLeft) || flag2)
		{
			AddControlIndex(-1);
			ApplyInputEvent();
			result = true;
		}
		else if (inputManager.ControllerButtonJustPressed(GamepadButtonFlags.DPadRight) || flag)
		{
			AddControlIndex(1);
			ApplyInputEvent();
			result = true;
		}
		return result;
	}

	private void FocusOnlyOne(int controlType)
	{
		IsFocusedByJoypad = true;
		controlIndex = controlType;
		for (int i = 0; i < 3; i++)
		{
			if (i == controlType)
			{
				ButtonList[i].Focus();
			}
			else
			{
				ButtonList[i].LostFocus();
			}
		}
	}

	private void ApplyInputEvent()
	{
		try
		{
			if (base.InvokeRequired)
			{
				Invoke((Action)delegate
				{
					ApplyInputEvent();
				});
			}
			else
			{
				Sound.PlayInstance(SoundManager.SoundType.Cursor);
				FocusOnlyOne(controlIndex);
			}
		}
		catch
		{
		}
	}

	private void Select_CallEventHandlerFormInputThread()
	{
		try
		{
			if (base.InvokeRequired)
			{
				Invoke((Action)delegate
				{
					Select_CallEventHandlerFormInputThread();
				});
			}
			else
			{
				CommitButton();
			}
		}
		catch
		{
		}
	}

	private void CommitButton()
	{
		switch (controlIndex)
		{
		case 0:
			playBtn_Click(this, null);
			break;
		case 1:
			settingBtn_Click(this, null);
			break;
		case 2:
			ExitBtn_Click();
			break;
		}
	}

	private void Cancel_CallEventHandlerFormInputThread()
	{
		try
		{
			if (base.InvokeRequired)
			{
				Invoke((Action)delegate
				{
					Cancel_CallEventHandlerFormInputThread();
				});
			}
			else
			{
				Console.Write("Close called : ");
				Close();
			}
		}
		catch
		{
		}
	}

	private bool IsContain(Control ctrl, Point pos)
	{
		return ctrl.ClientRectangle.Contains(pos);
	}

	private void playBtn_MouseEnter(object sender, EventArgs e)
	{
	}

	private void playBtn_MouseLeave(object sender, EventArgs e)
	{
	}

	private void settingBtn_MouseEnter(object sender, EventArgs e)
	{
	}

	private void settingBtn_MouseLeave(object sender, EventArgs e)
	{
	}

	private void ShowWindow(bool isShow)
	{
		if (isShow)
		{
			IsShowing = true;
			Show();
		}
		else
		{
			IsShowing = false;
			Hide();
		}
	}

	private void ActivateLauncher(bool isActive)
	{
		ShowWindow(isActive);
		SetReadInput(isActive);
	}

	private void StartGame()
	{
		ActivateLauncher(isActive: false);
		if (configFile.Launch7thHeaven == 1 && Get7thHeavenPath() != null)
		{
			Launch7thHeaven();
		}
		else if (!launch_FF7Launcher())
		{
		}
		FocusOnlyOne(0);
		ActivateLauncher(isActive: true);
	}

	private void Launch7thHeaven()
	{
		try
		{
			string path = Get7thHeavenPath();
			if (path != null)
			{
				Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to launch 7th Heaven: " + ex.Message, Program.Title);
		}
	}

	private static async void SleepAsync()
	{
		await Task.Delay(1000);
	}

	private void playBtn_Click(object sender, EventArgs e)
	{
		try
		{
			if (!CanPressPlay)
			{
				return;
			}
			CanPressPlay = false;
			Sound.PlayInstance(SoundManager.SoundType.Decide);
			playBtn.BackgroundImage = BTN_Play_02_Selected;
			ButtonPress(CustomPlayButton, value: true);
			CustomPlayButton.ForcePaint(CreateGraphics());
			Thread.Sleep(500);
			StartGame();
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, Program.Title);
			ButtonPress(CustomPlayButton, value: false);
			ActivateLauncher(isActive: true);
		}
		Task.Delay(1000).ContinueWith((Task c) => CanPressPlay = true);
	}

	private void ExitBtn_Click()
	{
		Sound.PlayInstance(SoundManager.SoundType.ExitButton);
		ExitButton.BackgroundImage = BTN_Exit_02_Selected;
		ButtonPress(CustomExitButton, value: true);
		CustomExitButton.ForcePaint(CreateGraphics());
		Thread.Sleep(500);
		Close();
	}

	private void SetReadInput(bool isRead)
	{
		ReadInput = isRead;
	}

	private void SetImage(ImgType type)
	{
		switch (type)
		{
		case ImgType.Deactivate:
			settingBtn.BackgroundImage = BTN_Setting_00_Deactive;
			break;
		case ImgType.MouseOn:
			settingBtn.BackgroundImage = BTN_Setting_01_MouseOn;
			break;
		case ImgType.Selected:
			settingBtn.BackgroundImage = BTN_Setting_02_Selected;
			break;
		}
	}

	private void ButtonPress(CustomVisualButton button, bool value)
	{
		if (value)
		{
			button.Click();
			button.Lock();
		}
		else
		{
			button.UnLock();
			button.UpdateByMousePosition(Control.MouseButtons, Control.MousePosition);
		}
	}

	private void SettingButtonPress(bool value)
	{
		ButtonPress(CustomSettingButton, value);
	}

	private bool IsAdministrator()
	{
		WindowsIdentity current = WindowsIdentity.GetCurrent();
		WindowsPrincipal windowsPrincipal = new WindowsPrincipal(current);
		return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
	}

	private void settingBtn_Click(object sender, EventArgs e)
	{
		Sound.PlayInstance(SoundManager.SoundType.Decide);
		Setting setting = new Setting();
		SettingFormOpened = true;
		SetImage(ImgType.Selected);
		setting.m_Launcher = this;
		SetReadInput(isRead: false);
		setting.StartInput(inputManager, DXInput);
		setting.ShowDialog(this);
		SettingFormOpened = false;
		FocusOnlyOne(1);
		SetImage(ImgType.Deactivate);
		SetReadInput(isRead: true);
	}

	private static Process ProcStart(string procPath, string arg)
	{
		Process process = new Process();
		process.StartInfo.FileName = procPath;
		process.StartInfo.Arguments = arg;
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardInput = true;
		process.StartInfo.RedirectStandardOutput = true;
		process.StartInfo.RedirectStandardError = true;
		process.OutputDataReceived += Proc_OutputDataReceived;
		process.ErrorDataReceived += Proc_ErrorDataReceived;
		process.StartInfo.CreateNoWindow = true;
		return process;
	}

	private static void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
	{
		Trace.WriteLine(e.Data);
	}

	private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
	{
		Trace.WriteLine(e.Data);
	}

	private bool launch_FF7Launcher()
	{
		string text = "FFVII.exe";
		bool result = true;
		ProcessStartInfo processStartInfo = new ProcessStartInfo(text);
		processStartInfo.UseShellExecute = true;
		processStartInfo.ErrorDialog = true;
		processStartInfo.ErrorDialogParentHandle = base.Handle;
		try
		{
			if (!File.Exists(text))
			{
				string message = new FileNotFoundException().Message;
				throw new FileNotFoundException(message + " :  " + text);
			}
			Process process;
			if (languageInstalled == "jp")
			{
				processStartInfo.Arguments = "jp";
				process = Process.Start(processStartInfo);
				Trace.WriteLine("Program Started JP : " + configFile.m_language + " " + DateTime.Now.ToString());
			}
			else
			{
				process = Process.Start(processStartInfo);
				Trace.WriteLine("Program Started EFIGS : " + configFile.m_language + " " + DateTime.Now.ToString());
			}
			process.WaitForExit();
			Trace.WriteLine("Program End : " + DateTime.Now.ToString());
			Trace.WriteLine("exe : " + Environment.CurrentDirectory + text);
			string[] files = Directory.GetFiles(Environment.CurrentDirectory, "*.*", SearchOption.AllDirectories);
			foreach (string message2 in files)
			{
				Trace.WriteLine(message2);
			}
		}
		catch (Exception)
		{
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(FF7Launcher));
			MessageBox.Show(componentResourceManager.GetString("FileMissing"), Program.Title);
			result = false;
		}
		finally
		{
			Trace.WriteLine("MousePosition : " + Control.MousePosition.ToString());
			configFile.Read("config.txt");
			CustomPlayButton.UnLock();
			TickMouse(Control.MousePosition);
		}
		return result;
	}

	private void FF7Launcher_FormClosing(object sender, FormClosingEventArgs e)
	{
	}

	private void FF7Launcher_FormClosed(object sender, FormClosedEventArgs e)
	{
		Running = false;
		SteamMan.Dispose();
		SoundManager.DisposeXAudio();
		Sound.Dispose();
		if (dinput != null)
		{
			dinput.UnAcquire();
		}
		if (device != null)
		{
			device.Dispose();
		}
		if (swapChain != null)
		{
			swapChain.Dispose();
		}
		device = null;
		swapChain = null;
	}

	private void timer1_Tick(object sender, EventArgs e)
	{
		Point location = PointToClient(Cursor.Position);
		TickMouse(location);
	}

	private void settingBtn_MouseMove(object sender, MouseEventArgs e)
	{
	}

	private void FF7Launcher_MouseDown(object sender, MouseEventArgs e)
	{
		Console.WriteLine("MouseDown");
		if (!IsShowing || !CanPressPlay)
		{
			return;
		}
		foreach (CustomVisualButton button in ButtonList)
		{
			button.OnClicked(Control.MouseButtons, e.Location);
		}
	}

	private void FF7Launcher_MouseUp(object sender, MouseEventArgs e)
	{
		foreach (CustomVisualButton button in ButtonList)
		{
			button.OnMouseUp(Control.MouseButtons, e.Location);
		}
	}

	private void FF7Launcher_MouseMove(object sender, MouseEventArgs e)
	{
	}

	private void TickMouse(Point location)
	{
		foreach (CustomVisualButton button in ButtonList)
		{
			button.TickMouse(Control.MouseButtons, location);
		}
	}

	public void PlaySound(SoundManager.SoundType type, float volume = -1f)
	{
		Sound.PlayInstance(type, volume);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		if (disposing)
		{
			if (BTN_Play_00_Deactive != null)
			{
				BTN_Play_00_Deactive.Dispose();
			}
			if (BTN_Play_01_MouseOn != null)
			{
				BTN_Play_01_MouseOn.Dispose();
			}
			if (BTN_Setting_00_Deactive != null)
			{
				BTN_Setting_00_Deactive.Dispose();
			}
			if (BTN_Setting_01_MouseOn != null)
			{
				BTN_Setting_01_MouseOn.Dispose();
			}
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FF7_Launcher.FF7Launcher));
		this.playBtn = new System.Windows.Forms.Button();
		this.settingBtn = new System.Windows.Forms.Button();
		this.timer1 = new System.Windows.Forms.Timer(this.components);
		this.ExitButton = new System.Windows.Forms.Button();
		base.SuspendLayout();
		this.playBtn.BackColor = System.Drawing.SystemColors.ControlText;
		this.playBtn.BackgroundImage = FF7_Launcher.Properties.Resources.UI_HUD_Launcher_BTN_play_00;
		resources.ApplyResources(this.playBtn, "playBtn");
		this.playBtn.FlatAppearance.BorderSize = 0;
		this.playBtn.ForeColor = System.Drawing.Color.White;
		this.playBtn.Name = "playBtn";
		this.playBtn.UseVisualStyleBackColor = false;
		this.playBtn.Click += new System.EventHandler(playBtn_Click);
		this.playBtn.MouseEnter += new System.EventHandler(playBtn_MouseEnter);
		this.playBtn.MouseLeave += new System.EventHandler(playBtn_MouseLeave);
		this.settingBtn.BackColor = System.Drawing.SystemColors.ControlText;
		this.settingBtn.BackgroundImage = FF7_Launcher.Properties.Resources.UI_HUD_Launcher_BTN_Setting_00;
		resources.ApplyResources(this.settingBtn, "settingBtn");
		this.settingBtn.FlatAppearance.BorderSize = 0;
		this.settingBtn.ForeColor = System.Drawing.Color.White;
		this.settingBtn.Name = "settingBtn";
		this.settingBtn.UseVisualStyleBackColor = false;
		this.settingBtn.Click += new System.EventHandler(settingBtn_Click);
		this.settingBtn.MouseEnter += new System.EventHandler(settingBtn_MouseEnter);
		this.settingBtn.MouseLeave += new System.EventHandler(settingBtn_MouseLeave);
		this.settingBtn.MouseMove += new System.Windows.Forms.MouseEventHandler(settingBtn_MouseMove);
		this.timer1.Tick += new System.EventHandler(timer1_Tick);
		this.ExitButton.BackgroundImage = FF7_Launcher.Properties.Resources.UI_HUD_Launcher_BTN_exit_00_EN;
		resources.ApplyResources(this.ExitButton, "ExitButton");
		this.ExitButton.ForeColor = System.Drawing.Color.White;
		this.ExitButton.Name = "ExitButton";
		this.ExitButton.UseVisualStyleBackColor = true;
		resources.ApplyResources(this, "$this");
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.Black;
		this.BackgroundImage = FF7_Launcher.Properties.Resources.UI_HUD_Launcher_BG;
		base.Controls.Add(this.ExitButton);
		base.Controls.Add(this.settingBtn);
		base.Controls.Add(this.playBtn);
		this.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "FF7Launcher";
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FF7Launcher_FormClosing);
		base.FormClosed += new System.Windows.Forms.FormClosedEventHandler(FF7Launcher_FormClosed);
		base.MouseMove += new System.Windows.Forms.MouseEventHandler(FF7Launcher_MouseMove);
		base.MouseUp += new System.Windows.Forms.MouseEventHandler(FF7Launcher_MouseUp);
		base.ResumeLayout(false);
	}
}
