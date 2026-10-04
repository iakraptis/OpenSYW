using System;
using System.IO;
using System.Threading.Tasks;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Syw.AssetInstaller;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// The syw-content screen: shown when the converted game content is missing (first launch) or from the main menu's
	// Manage Content button. Finds the player's Seven Years War folder (auto-detected, or picked with Browse), converts it
	// into the support folder on a background thread, then starts the game.
	public class SywContentLogic : ChromeLogic
	{
		const string GameMod = "syw";

		enum State { Searching, NotFound, Found, Installed, Installing, Failed }

		readonly string modFolder;
		State state;
		string gamePath;
		string error;
		FolderPicker picker;

		// Written by the install thread, read by the UI.
		volatile int progress;
		volatile string currentStage;
		Task install;

		[ObjectCreator.UseCtor]
		public SywContentLogic(Widget widget)
		{
			modFolder = Game.Mods[GameMod].Package.Name;
			var panel = widget.Get("PANEL");

			panel.Get<LabelWidget>("MESSAGE").GetText = Message;
			panel.Get<LabelWidget>("PATH").GetText = () => gamePath ?? "";

			var bar = panel.Get<ProgressBarWidget>("PROGRESS");
			bar.IsVisible = () => state == State.Installing;
			bar.GetPercentage = () => progress;

			var status = panel.Get<LabelWidget>("STATUS");
			status.GetText = () => state == State.Installing
				? FluentProvider.GetMessage("syw-content-converting", "stage", currentStage ?? "")
				: state == State.Failed ? error : "";
			status.GetColor = () => state == State.Failed ? Color.FromArgb(0xFF, 0x60, 0x60) : status.TextColor;

			var browse = panel.Get<ButtonWidget>("BROWSE_BUTTON");
			browse.IsVisible = OperatingSystem.IsWindows;
			browse.IsDisabled = () => state == State.Installing || picker != null;
			browse.OnClick = () =>
			{
				if (OperatingSystem.IsWindows())
					picker = new FolderPicker(FluentProvider.GetMessage("syw-content-browse-title"));
			};

			var installButton = panel.Get<ButtonWidget>("INSTALL_BUTTON");
			installButton.GetText = () => FluentProvider.GetMessage(state == State.Installed ? "button-syw-content-reinstall" : "button-syw-content-install");
			installButton.IsDisabled = () => gamePath == null || state == State.Installing || picker != null;
			installButton.OnClick = StartInstall;

			// Back to the game when the content is there, otherwise the only way out is to quit.
			var installed = IsInstalled();
			var back = panel.Get<ButtonWidget>("BACK_BUTTON");
			back.GetText = () => FluentProvider.GetMessage(IsInstalled() ? "button-syw-content-play" : "button-syw-content-quit");
			back.IsDisabled = () => state == State.Installing;
			back.OnClick = () =>
			{
				if (IsInstalled())
					Game.RunAfterTick(() => Game.InitializeMod(GameMod, new Arguments()));
				else
					Game.Exit();
			};

			gamePath = SywContent.Detect();
			state = installed ? State.Installed : gamePath != null ? State.Found : State.NotFound;
		}

		static bool IsInstalled() => File.Exists(Path.Combine(SywContent.Folder, SywContent.ManifestFile));

		string Message()
		{
			switch (state)
			{
				case State.Installed: return FluentProvider.GetMessage("syw-content-installed");
				case State.Found: return FluentProvider.GetMessage("syw-content-found");
				case State.Installing: return FluentProvider.GetMessage("syw-content-installing");
				case State.Failed: return FluentProvider.GetMessage("syw-content-failed");
				default: return FluentProvider.GetMessage("syw-content-not-found");
			}
		}

		void StartInstall()
		{
			var source = gamePath;
			state = State.Installing;
			progress = 0;
			currentStage = null;

			install = Task.Run(() =>
			{
				// Start from an empty folder: it is ours alone, and files an older converter wrote must not linger.
				if (Directory.Exists(SywContent.Folder))
					Directory.Delete(SywContent.Folder, true);

				SywContent.Run(source, SywContent.Folder, modFolder, SywContent.PlayerStages, progress: (stage, index, count) =>
				{
					currentStage = stage.Description;
					progress = 100 * index / count;
				});

				progress = 100;
			});
		}

		public override void Tick()
		{
			if (picker != null && picker.IsDone)
			{
				var chosen = picker.Result;
				picker = null;
				if (chosen != null)
				{
					if (GameFolder.LooksLike(chosen))
					{
						gamePath = chosen;
						state = IsInstalled() ? State.Installed : State.Found;
					}
					else
					{
						error = FluentProvider.GetMessage("syw-content-not-game-folder", "folder", chosen);
						state = State.Failed;
					}
				}
			}

			if (install != null && install.IsCompleted)
			{
				var task = install;
				install = null;
				if (task.IsFaulted)
				{
					var e = task.Exception?.GetBaseException();
					Log.Write("debug", $"Content install failed: {e}");
					error = e?.Message ?? "";
					state = State.Failed;
				}
				else
					Game.RunAfterTick(() => Game.InitializeMod(GameMod, new Arguments()));
			}
		}
	}
}
