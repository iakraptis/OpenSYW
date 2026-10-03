using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.UtilityCommands
{
	// utility.cmd syw --dump-rules FILE
	// Writes every actor's fully resolved rules (inheritance applied): its traits and each trait's field values, sorted.
	// Two dumps can be compared to prove that a reorganisation of the rule files changes nothing in game.
	sealed class DumpRulesCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--dump-rules";

		bool IUtilityCommand.ValidateArguments(string[] args) => args.Length == 2;

		[Desc("FILE", "Write every actor's resolved traits and field values to FILE, for comparing rule refactors.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// Rule loading looks up the global mod data (as the engine's own utility commands set it).
			Game.ModData = utility.ModData;
			var rules = utility.ModData.DefaultRules;
			var lines = new List<string>();
			foreach (var (name, actor) in rules.Actors.OrderBy(a => a.Key, StringComparer.Ordinal))
			{
				lines.Add($"{name}:");
				var traits = actor.TraitInfos<TraitInfo>()
					.Select(t => (Key: t.GetType().Name + (string.IsNullOrEmpty(t.InstanceName) ? "" : "@" + t.InstanceName), Info: t))
					.ToList();

				// The order traits are created in (it decides e.g. which voice or armament comes first).
				lines.Add($"\t(order) {string.Join(", ", traits.Select(t => t.Key))}");
				foreach (var (key, info) in traits.OrderBy(t => t.Key, StringComparer.Ordinal))
				{
					lines.Add($"\t{key}");
					foreach (var field in info.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal))
						lines.Add($"\t\t{field.Name}: {Format(field.GetValue(info))}");
				}
			}

			File.WriteAllLines(args[1], lines);
			Console.WriteLine($"Wrote {rules.Actors.Count} actors to {args[1]}");
		}

		static string Format(object value)
		{
			try
			{
				return FieldSaver.FormatValue(value);
			}
			catch (Exception)
			{
				return value?.ToString() ?? "";
			}
		}
	}
}
