using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.UtilityCommands
{
	// utility.cmd syw --dump-rules FILE
	// Writes every actor's fully resolved rules (inheritance applied): its traits and each trait's field values, sorted;
	// then every weapon with its projectile and warheads, and the merged sequence definitions.
	// Two dumps can be compared to prove that a reorganisation of the rule files changes nothing in game.
	sealed class DumpRulesCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--dump-rules";

		bool IUtilityCommand.ValidateArguments(string[] args) => args.Length == 2;

		[Desc("FILE", "Write every actor's resolved traits and field values, every weapon and the sequences to FILE, for comparing rule refactors.")]
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

			foreach (var (name, weapon) in rules.Weapons.OrderBy(w => w.Key, StringComparer.Ordinal))
			{
				lines.Add($"weapon {name}:");
				AddFields(lines, "\t", weapon);
				if (weapon.Projectile != null)
				{
					lines.Add($"\tProjectile {weapon.Projectile.GetType().Name}");
					AddFields(lines, "\t\t", weapon.Projectile);
				}

				// Warheads in their order (it decides which applies first).
				foreach (var warhead in weapon.Warheads)
				{
					lines.Add($"\tWarhead {warhead.GetType().Name}");
					AddFields(lines, "\t\t", warhead);
				}
			}

			// The sequence definitions as the sequence loader receives them: every file merged, entries sorted.
			var sequences = MiniYaml.Load(utility.ModData.DefaultFileSystem, utility.ModData.Manifest.Sequences, null);
			foreach (var image in sequences.OrderBy(n => n.Key, StringComparer.Ordinal))
			{
				lines.Add($"sequence {image.Key}: {image.Value.Value}");
				AddYaml(lines, "\t", image.Value);
			}

			File.WriteAllLines(args[1], lines);
			Console.WriteLine($"Wrote {rules.Actors.Count} actors, {rules.Weapons.Count} weapons and {sequences.Count} images to {args[1]}");
		}

		// A yaml node's children, sorted by key, recursively.
		static void AddYaml(List<string> lines, string indent, MiniYaml yaml)
		{
			foreach (var child in yaml.Nodes.OrderBy(n => n.Key, StringComparer.Ordinal))
			{
				lines.Add($"{indent}{child.Key}: {child.Value.Value}");
				AddYaml(lines, indent + "\t", child.Value);
			}
		}

		// Public fields of a weapon, projectile or warhead, sorted; nested objects (Projectile, Warheads) are listed apart.
		static void AddFields(List<string> lines, string indent, object info)
		{
			foreach (var field in info.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal))
				if (field.Name != "Projectile" && field.Name != "Warheads")
					lines.Add($"{indent}{field.Name}: {Format(field.GetValue(info))}");
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
