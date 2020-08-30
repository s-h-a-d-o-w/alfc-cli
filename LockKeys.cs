using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace AeroCtl.Rgb.LockKeys
{
	static class Program
	{
		private const int WH_KEYBOARD_LL = 13;
		private const int WM_KEYDOWN = 0x0100;
		private const int WM_KEYUP = 0x0101;

		private const int VK_CAPITAL = 0x14;
		private const int VK_NUMLOCK = 0x90;
		private const int VK_SCROLL = 0x91;

		private static readonly LowLevelKeyboardProc proc = hookCallback;

		private static IntPtr hookID = IntPtr.Zero;
		private static SynchronizationContext syncContext;

		public static async Task Main()
		{
			await Effect.UpdateAsync();

			hookID = setHook(proc);

			SystemEvents.SessionSwitch += onSessionSwitch;

			using (Timer lidCheckTimer = new Timer())
			{
				lidCheckTimer.Interval = 5000;
				lidCheckTimer.Tick += (s, e) => { update(); };
				lidCheckTimer.Start();

				Application.Run();
			}

			SystemEvents.SessionSwitch -= onSessionSwitch;

			UnhookWindowsHookEx(hookID);
		}

		private static void update()
		{
			syncContext = SynchronizationContext.Current ?? syncContext;
			Effect.UpdateAsync().ContinueWith(t =>
			{
				syncContext?.Post(_ =>
				{
					t.Wait();
				}, null);
			});
		}

		private static void onSessionSwitch(object sender, SessionSwitchEventArgs e)
		{
			// Re-apply the effect. It seems that it gets corrupted sometimes for unknown reasons.
			if (e.Reason == SessionSwitchReason.SessionUnlock ||
			    e.Reason == SessionSwitchReason.SessionLock)
				update();
		}

		private static IntPtr setHook(LowLevelKeyboardProc proc)
		{
			using (Process curProcess = Process.GetCurrentProcess())
			using (ProcessModule curModule = curProcess.MainModule)
			{
				return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
			}
		}

		private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

		private static IntPtr hookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0 && wParam == (IntPtr)WM_KEYUP)
			{
				int vkCode = Marshal.ReadInt32(lParam);
				if (vkCode == VK_CAPITAL ||
				    vkCode == VK_NUMLOCK || 
				    vkCode == VK_SCROLL)
				{
					update();
				}
			}

			return CallNextHookEx(hookID, nCode, wParam, lParam);
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWindowsHookEx(IntPtr hhk);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);
	}
	
	[Flags]
	internal enum EffectState
	{
		Caps = 1,
		Num = 2,
		Scroll = 4,
		LidClosed = 8,
	}

	internal static class Effect
	{
		//private static readonly Color baseColor = Color.FromArgb(0, 100, 40);
		//private static readonly Color highlightColor = Color.FromArgb(0, 255, 30);
		private static readonly Color baseColor = Color.FromArgb(60, 255, 90);
		private static readonly Color highlightColor = Color.FromArgb(0, 200, 255);

		private const int capsLockKey = 8;
		private const int numLockKey = 100;

		private static readonly int[] numPadKeys =
		{
			97, 98, 99,
			102, 103, 104, 105,
			108, 109, 110, 111,
		};

		/// <summary>
		/// Update state and apply.
		/// </summary>
		/// <returns></returns>
		public static async Task<bool> UpdateAsync()
		{
			await Task.Delay(5); // Small delay so Control.IsKeyLocked returns the correct value.

			// Sometimes the keyboard controller seems to get disconnected (e.g. after wake up from sleep), so we try a couple of times.
			for (int i = 9; i >= 0; --i)
			{
				using (Aero aero = new Aero())
				{
					// Read current keyboard state.
					EffectState state = 0;

					if (Control.IsKeyLocked(Keys.CapsLock))
						state |= EffectState.Caps;
					else
						state &= ~EffectState.Caps;

					if (Control.IsKeyLocked(Keys.NumLock))
						state |= EffectState.Num;
					else
						state &= ~EffectState.Num;

					if (Control.IsKeyLocked(Keys.Scroll))
						state |= EffectState.Scroll;
					else
						state &= ~EffectState.Scroll;

					if (await aero.Display.GetLidStatus() == LidStatus.Closed)
						state |= EffectState.LidClosed;
					else
						state &= ~EffectState.LidClosed;

					//Console.WriteLine(state);

					// Try to apply the effect.
					if (await applyAsync(aero.Keyboard.Rgb, state, i > 0))
						return true;
				}

				await Task.Delay(1000);
			}

			return false;
		}

		/// <summary>
		/// Apply the effect.
		/// </summary>
		private static async Task<bool> applyAsync(IRgbController rgb, EffectState state, bool suppressException)
		{
			// Check if the RGB controller even exists.
			if (rgb == null)
				return false;

			if ((state & EffectState.LidClosed) != 0)
			{
				// Turn off when lid is closed.
				try
				{
					int brightness = (await rgb.GetEffectAsync()).Brightness;
					await rgb.SetEffectAsync(new RgbEffect
					{
						Type = RgbEffectType.Static,
						Color = RgbEffectColor.Black,
						Brightness = brightness,
					});

					return true;
				}
				catch (Win32Exception) when (suppressException)
				{
					return false;
				}
				catch (IOException) when (suppressException)
				{
					return false;
				}
			}

			byte[] image = new byte[512];
			// void setColor(int key, Color color)
			// {
			// 	image[4 * key + 0] = (byte)key;
			// 	image[4 * key + 1] = color.R;
			// 	image[4 * key + 2] = color.G;
			// 	image[4 * key + 3] = color.B;
			// }

			// Fill with base color.
			for (int i = 0; i < 128; ++i)
				setColor(i, baseColor);

			// Apply changes to certain keys / areas.
			setColor(capsLockKey, (state & EffectState.Caps) != 0 ? highlightColor : baseColor);
			setColor(numLockKey, (state & EffectState.Scroll) != 0 ? highlightColor : baseColor);
			foreach (int k in numPadKeys)
				setColor(k, (state & EffectState.Num) == 0 ? highlightColor : baseColor);

			// Try to apply the effect.
			// Under certain conditions this seems to fail because the USB device gets disconnected (e.g. waking from sleep).
			// In that case it will exit the inner loop in order to try again.
			try
			{
				// Read current brightness from controller. This can be changed independently from this app by the user through
				// the keyboard brightness shortcut (Fn + Space).
				int brightness = (await rgb.GetEffectAsync()).Brightness;

				// Set new image.
				await rgb.SetImageAsync(0, image);
				await rgb.SetEffectAsync(new RgbEffect
				{
					Type = RgbEffectType.Custom0,
					Brightness = brightness,
				});

				return true;
			}
			catch (Win32Exception) when (suppressException)
			{
				return false;
			}
			catch (IOException) when (suppressException)
			{
				return false;
			}
		}
	}
}
