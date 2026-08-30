using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using RathalOS.Data.Context;
using RathalOS.Infra;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Color = SixLabors.ImageSharp.Color;
using Image = SixLabors.ImageSharp.Image;

namespace RathalOS.Data.Models
{
	public class MHHCard
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public Guid Guid { get; set; }
		public string CardId { get; set; } = string.Empty;
		public string CardName { get; set; } = string.Empty;
		public string CardNameJP { get; set; } = string.Empty;
		public string CardType { get; set; } = string.Empty;
		public string CardDescription { get; set; } = string.Empty;
		public string Power { get; set; } = string.Empty;
		public string Rank { get; set; } = string.Empty;
		public string? HunterWeapon { get; set; }
		public string? HunterArmor { get; set; }
		public CardDeco Decoration { get; set; }
		public CardRarity Rarity { get; set; }
	}

	public class MHHCardPackage
	{
		public byte[] CardBytes { get; set; } = [];
		public MHHCard Card { get; set; } = new();
		private static Random _rand { get; set; } = new();
		private static readonly Dictionary<string, string> _cardMaps = new()
			{
				{ "B01-01", "Common" },
				{ "B01-02", "Common" },
				{ "B01-03", "Common" },
				{ "B01-04", "Uncommon" },
				{ "B01-05", "Uncommon" },
				{ "B01-06", "Rare" },
				{ "B01-07", "Rare" },
				{ "B01-08", "Common" },
				{ "B01-09", "Ultra Rare" },
				{ "B01-10", "Uncommon" },
				{ "B01-11", "Uncommon" },
				{ "B01-12", "Common" },
				{ "B01-13", "Common" },
				{ "B01-14", "Rare" },
				{ "B01-15", "Common" },
				{ "B01-16", "Ultra Rare" },
				{ "B01-17", "Ultra Rare" },
				{ "B01-18", "Common" },
				{ "B01-19", "Uncommon" },
				{ "B01-20", "Uncommon" },
				{ "B01-21", "Uncommon" },
				{ "B01-22", "Rare" },
				{ "B01-23", "Ultra Rare" },
				{ "B01-24", "Common" },
				{ "B01-25", "Common" },
				{ "B01-26", "Rare" },
				{ "B01-27", "Ultra Rare" },
				{ "B01-28", "Rare" },
				{ "B01-29", "Common" },
				{ "B01-30", "Common" },
				{ "B01-31", "Rare" },
				{ "B01-32", "Common" },
				{ "B01-33", "Uncommon" },
				{ "B01-34", "Common" },
				{ "B01-35", "Rare" },
				{ "B01-36", "Common" },
				{ "B01-37", "Common" },
				{ "B01-38", "Uncommon" },
				{ "B01-39", "Rare" },
				{ "B01-40", "Common" },
				{ "B01-41", "Uncommon" },
				{ "B01-42", "Common" },
				{ "B01-43", "Rare" },
				{ "B01-44", "Common" },
				{ "B01-45", "Common" },
				{ "B01-46", "Uncommon" },
				{ "B01-47", "Rare" },
				{ "B01-48", "Common" },
				{ "B01-49", "Common" },
				{ "B01-50", "Uncommon" },
				{ "B01-51", "Rare" },
				{ "B01-52", "Common" },
				{ "B01-53", "Common" },
				{ "B01-54", "Uncommon" },
				{ "B01-55", "Rare" },
				{ "B01-56", "Common" },
				{ "B01-57", "Common" },
				{ "B01-58", "Uncommon" },
				{ "B01-59", "Rare" },
				{ "B01-60", "Common" },
				{ "B01-61", "Common" },
				{ "B01-62", "Uncommon" },
				{ "B01-63", "Rare" },
				{ "B01-64", "Common" },
				{ "B01-65", "Common" },
				{ "B01-66", "Uncommon" },
				{ "B01-67", "Rare" },
				{ "B01-68", "Common" },
				{ "B01-69", "Common" },
				{ "B01-70", "Rare" },
				{ "B01-71", "Common" },
				{ "B01-72", "Uncommon" },
				{ "B01-73", "Uncommon" },
				{ "B01-74", "Common" },
				{ "B01-75", "Common" },
				{ "B01-76", "Common" },
				{ "B01-77", "Common" },
				{ "B01-78", "Common" },
				{ "B01-79", "Common" },
				{ "B01-80", "Common" },
				{ "B01-81", "Common" },
				{ "B01-82", "Common" },
				{ "B01-83", "Common" },
				{ "B01-84", "Common" },
				{ "B01-85", "Uncommon" },
				{ "B01-86", "Common" },
				{ "B01-87", "Common" },
				{ "B01-88", "Common" },
				{ "B01-89", "Uncommon" },
				{ "B01-90", "Common" },
				{ "B02-01", "Common" },
				{ "B02-02", "Common" },
				{ "B02-03", "Uncommon" },
				{ "B02-04", "Common" },
				{ "B02-05", "Uncommon" },
				{ "B02-06", "Uncommon" },
				{ "B02-07", "Rare" },
				{ "B02-08", "Ultra Rare" },
				{ "B02-09", "Rare" },
				{ "B02-10", "Ultra Rare" },
				{ "B02-11", "Common" },
				{ "B02-12", "Rare" },
				{ "B02-13", "Common" },
				{ "B02-14", "Ultra Rare" },
				{ "B02-15", "Uncommon" },
				{ "B02-16", "Common" },
				{ "B02-17", "Uncommon" },
				{ "B02-18", "Uncommon" },
				{ "B02-19", "Rare" },
				{ "B02-20", "Rare" },
				{ "B02-21", "Rare" },
				{ "B02-22", "Rare" },
				{ "B02-23", "Rare" },
				{ "B02-24", "Rare" },
				{ "B02-25", "Common" },
				{ "B02-26", "Uncommon" },
				{ "B02-27", "Ultra Rare" },
				{ "B02-28", "Ultra Rare" },
				{ "B02-29", "Common" },
				{ "B02-30", "Uncommon" },
				{ "B02-31", "Rare" },
				{ "B02-32", "Common" },
				{ "B02-33", "Uncommon" },
				{ "B02-34", "Rare" },
				{ "B02-35", "Common" },
				{ "B02-36", "Uncommon" },
				{ "B02-37", "Rare" },
				{ "B02-38", "Common" },
				{ "B02-39", "Uncommon" },
				{ "B02-40", "Rare" },
				{ "B02-41", "Common" },
				{ "B02-42", "Uncommon" },
				{ "B02-43", "Rare" },
				{ "B02-44", "Common" },
				{ "B02-45", "Uncommon" },
				{ "B02-46", "Rare" },
				{ "B02-47", "Common" },
				{ "B02-48", "Uncommon" },
				{ "B02-49", "Rare" },
				{ "B02-50", "Common" },
				{ "B02-51", "Uncommon" },
				{ "B02-52", "Rare" },
				{ "B02-53", "Common" },
				{ "B02-54", "Uncommon" },
				{ "B02-55", "Rare" },
				{ "B02-56", "Common" },
				{ "B02-57", "Uncommon" },
				{ "B02-58", "Rare" },
				{ "B02-59", "Common" },
				{ "B02-60", "Uncommon" },
				{ "B02-61", "Rare" },
				{ "B02-62", "Common" },
				{ "B02-63", "Common" },
				{ "B02-64", "Common" },
				{ "B02-65", "Common" },
				{ "B02-66", "Common" },
				{ "B02-67", "Common" },
				{ "B02-68", "Common" },
				{ "B02-69", "Common" },
				{ "B02-70", "Common" },
				{ "B02-71", "Common" },
				{ "B02-72", "Uncommon" },
				{ "B02-73", "Common" },
				{ "B02-74", "Common" },
				{ "B02-75", "Uncommon" },
				{ "B02-76", "Common" },
				{ "B02-77", "Common" },
				{ "B03-01", "Common" },
				{ "B03-02", "Rare" },
				{ "B03-03", "Common" },
				{ "B03-04", "Common" },
				{ "B03-05", "Common" },
				{ "B03-06", "Rare" },
				{ "B03-07", "Rare" },
				{ "B03-08", "Common" },
				{ "B03-09", "Rare" },
				{ "B03-10", "Ultra Rare" },
				{ "B03-11", "Ultra Rare" },
				{ "B03-12", "Common" },
				{ "B03-13", "Common" },
				{ "B03-14", "Uncommon" },
				{ "B03-15", "Uncommon" },
				{ "B03-16", "Common" },
				{ "B03-17", "Common" },
				{ "B03-18", "Common" },
				{ "B03-19", "Common" },
				{ "B03-20", "Rare" },
				{ "B03-21", "Uncommon" },
				{ "B03-22", "Uncommon" },
				{ "B03-23", "Common" },
				{ "B03-24", "Rare" },
				{ "B03-25", "Rare" },
				{ "B03-26", "Rare" },
				{ "B03-27", "Uncommon" },
				{ "B03-28", "Ultra Rare" },
				{ "B03-29", "Ultra Rare" },
				{ "B03-30", "Uncommon" },
				{ "B03-31", "Uncommon" },
				{ "B03-32", "Rare" },
				{ "B03-33", "Common" },
				{ "B03-34", "Uncommon" },
				{ "B03-35", "Rare" },
				{ "B03-36", "Common" },
				{ "B03-37", "Uncommon" },
				{ "B03-38", "Rare" },
				{ "B03-39", "Common" },
				{ "B03-40", "Uncommon" },
				{ "B03-41", "Rare" },
				{ "B03-42", "Common" },
				{ "B03-43", "Rare" },
				{ "B03-44", "Rare" },
				{ "B03-45", "Common" },
				{ "B03-46", "Uncommon" },
				{ "B03-47", "Rare" },
				{ "B03-48", "Common" },
				{ "B03-49", "Uncommon" },
				{ "B03-50", "Rare" },
				{ "B03-51", "Common" },
				{ "B03-52", "Uncommon" },
				{ "B03-53", "Rare" },
				{ "B03-54", "Common" },
				{ "B03-55", "Uncommon" },
				{ "B03-56", "Rare" },
				{ "B03-57", "Common" },
				{ "B03-58", "Uncommon" },
				{ "B03-59", "Rare" },
				{ "B03-60", "Common" },
				{ "B03-61", "Uncommon" },
				{ "B03-62", "Ultra Rare" },
				{ "B03-63", "Common" },
				{ "B03-64", "Uncommon" },
				{ "B03-65", "Common" },
				{ "B03-66", "Common" },
				{ "B03-67", "Common" },
				{ "B03-68", "Uncommon" },
				{ "B03-69", "Uncommon" },
				{ "B03-70", "Common" },
				{ "B03-71", "Common" },
				{ "B03-72", "Common" },
				{ "B03-73", "Common" },
				{ "B03-74", "Common" },
				{ "B03-75", "Rare" },
				{ "B03-76", "Uncommon" },
				{ "B03-77", "Common" },
				{ "B04-01", "Common" },
				{ "B04-02", "Common" },
				{ "B04-03", "Rare" },
				{ "B04-04", "Uncommon" },
				{ "B04-05", "Rare" },
				{ "B04-06", "Common" },
				{ "B04-07", "Uncommon" },
				{ "B04-08", "Rare" },
				{ "B04-09", "Ultra Rare" },
				{ "B04-10", "Ultra Rare" },
				{ "B04-11", "Common" },
				{ "B04-12", "Common" },
				{ "B04-13", "Common" },
				{ "B04-14", "Common" },
				{ "B04-15", "Rare" },
				{ "B04-16", "Uncommon" },
				{ "B04-17", "Common" },
				{ "B04-18", "Common" },
				{ "B04-19", "Rare" },
				{ "B04-20", "Uncommon" },
				{ "B04-21", "Rare" },
				{ "B04-22", "Common" },
				{ "B04-23", "Rare" },
				{ "B04-24", "Uncommon" },
				{ "B04-25", "Uncommon" },
				{ "B04-26", "Rare" },
				{ "B04-27", "Rare" },
				{ "B04-28", "Ultra Rare" },
				{ "B04-29", "Uncommon" },
				{ "B04-30", "Uncommon" },
				{ "B04-31", "Rare" },
				{ "B04-32", "Common" },
				{ "B04-33", "Uncommon" },
				{ "B04-34", "Rare" },
				{ "B04-35", "Common" },
				{ "B04-36", "Uncommon" },
				{ "B04-37", "Rare" },
				{ "B04-38", "Uncommon" },
				{ "B04-39", "Uncommon" },
				{ "B04-40", "Rare" },
				{ "B04-41", "Common" },
				{ "B04-42", "Uncommon" },
				{ "B04-43", "Ultra Rare" },
				{ "B04-44", "Common" },
				{ "B04-45", "Uncommon" },
				{ "B04-46", "Rare" },
				{ "B04-47", "Common" },
				{ "B04-48", "Uncommon" },
				{ "B04-49", "Rare" },
				{ "B04-50", "Common" },
				{ "B04-51", "Uncommon" },
				{ "B04-52", "Rare" },
				{ "B04-53", "Common" },
				{ "B04-54", "Uncommon" },
				{ "B04-55", "Rare" },
				{ "B04-56", "Common" },
				{ "B04-57", "Uncommon" },
				{ "B04-58", "Rare" },
				{ "B04-59", "Common" },
				{ "B04-60", "Ultra Rare" },
				{ "B04-61", "Rare" },
				{ "B04-62", "Common" },
				{ "B04-63", "Common" },
				{ "B04-64", "Common" },
				{ "B04-65", "Uncommon" },
				{ "B04-66", "Common" },
				{ "B04-67", "Common" },
				{ "B04-68", "Common" },
				{ "B04-69", "Common" },
				{ "B04-70", "Common" },
				{ "B04-71", "Common" },
				{ "B04-72", "Common" },
				{ "B04-73", "Common" },
				{ "B04-74", "Common" },
				{ "B04-75", "Common" },
				{ "B04-76", "Uncommon" },
				{ "B04-77", "Rare" },
				{ "B05-01", "Common" },
				{ "B05-02", "Common" },
				{ "B05-03", "Common" },
				{ "B05-04", "Common" },
				{ "B05-05", "Uncommon" },
				{ "B05-06", "Common" },
				{ "B05-07", "Uncommon" },
				{ "B05-08", "Common" },
				{ "B05-09", "Uncommon" },
				{ "B05-10", "Common" },
				{ "B05-11", "Ultra Rare" },
				{ "B05-12", "Rare" },
				{ "B05-13", "Common" },
				{ "B05-14", "Uncommon" },
				{ "B05-15", "Common" },
				{ "B05-16", "Common" },
				{ "B05-17", "Common" },
				{ "B05-18", "Common" },
				{ "B05-19", "Uncommon" },
				{ "B05-20", "Common" },
				{ "B05-21", "Common" },
				{ "B05-22", "Common" },
				{ "B05-23", "Rare" },
				{ "B05-24", "Common" },
				{ "B05-25", "Common" },
				{ "B05-26", "Common" },
				{ "B05-27", "Common" },
				{ "B05-28", "Common" },
				{ "B05-29", "Uncommon" },
				{ "B05-30", "Rare" },
				{ "B05-31", "Uncommon" },
				{ "B05-32", "Uncommon" },
				{ "B05-33", "Common" },
				{ "B05-34", "Uncommon" },
				{ "B05-35", "Ultra Rare" },
				{ "B05-36", "Rare" },
				{ "B05-37", "Ultra Rare" },
				{ "B05-38", "Rare" },
				{ "B05-39", "Rare" },
				{ "B05-40", "Common" },
				{ "B05-41", "Common" },
				{ "B05-42", "Rare" },
				{ "B05-43", "Common" },
				{ "B05-44", "Uncommon" },
				{ "B05-45", "Rare" },
				{ "B05-46", "Common" },
				{ "B05-47", "Common" },
				{ "B05-48", "Rare" },
				{ "B05-49", "Common" },
				{ "B05-50", "Uncommon" },
				{ "B05-51", "Ultra Rare" },
				{ "B05-52", "Common" },
				{ "B05-53", "Uncommon" },
				{ "B05-54", "Rare" },
				{ "B05-55", "Uncommon" },
				{ "B05-56", "Common" },
				{ "B05-57", "Uncommon" },
				{ "B05-58", "Common" },
				{ "B05-59", "Rare" },
				{ "B05-60", "Rare" },
				{ "B05-61", "Common" },
				{ "B05-62", "Uncommon" },
				{ "B05-63", "Rare" },
				{ "B05-64", "Common" },
				{ "B05-65", "Rare" },
				{ "B05-66", "Common" },
				{ "B05-67", "Common" },
				{ "B05-68", "Uncommon" },
				{ "B05-69", "Common" },
				{ "B05-70", "Common" },
				{ "B05-71", "Uncommon" },
				{ "B05-72", "Rare" },
				{ "B05-73", "Common" },
				{ "B05-74", "Uncommon" },
				{ "B05-75", "Uncommon" },
				{ "B05-76", "Common" },
				{ "B05-77", "Rare" },
				{ "B05-78", "Ultra Rare" },
				{ "B05-79", "Common" },
				{ "B05-80", "Common" },
				{ "B05-81", "Common" },
				{ "B05-82", "Common" },
				{ "B05-83", "Common" },
				{ "B05-84", "Common" },
				{ "B05-85", "Common" },
				{ "B05-86", "Rare" },
				{ "B05-87", "Common" },
				{ "B05-88", "Uncommon" },
				{ "B05-89", "Common" },
				{ "B05-90", "Common" },
				{ "B06-01", "Common" },
				{ "B06-02", "Common" },
				{ "B06-03", "Common" },
				{ "B06-04", "Rare" },
				{ "B06-05", "Ultra Rare" },
				{ "B06-06", "Rare" },
				{ "B06-07", "Common" },
				{ "B06-08", "Rare" },
				{ "B06-09", "Rare" },
				{ "B06-10", "Rare" },
				{ "B06-11", "Common" },
				{ "B06-12", "Common" },
				{ "B06-13", "Common" },
				{ "B06-14", "Common" },
				{ "B06-15", "Common" },
				{ "B06-16", "Common" },
				{ "B06-17", "Common" },
				{ "B06-18", "Common" },
				{ "B06-19", "Common" },
				{ "B06-20", "Common" },
				{ "B06-21", "Rare" },
				{ "B06-22", "Rare" },
				{ "B06-23", "Rare" },
				{ "B06-24", "Common" },
				{ "B06-25", "Rare" },
				{ "B06-26", "Ultra Rare" },
				{ "B06-27", "Ultra Rare" },
				{ "B06-28", "Uncommon" },
				{ "B06-29", "Rare" },
				{ "B06-30", "Uncommon" },
				{ "B06-31", "Uncommon" },
				{ "B06-32", "Common" },
				{ "B06-33", "Rare" },
				{ "B06-34", "Common" },
				{ "B06-35", "Uncommon" },
				{ "B06-36", "Uncommon" },
				{ "B06-37", "Common" },
				{ "B06-38", "Uncommon" },
				{ "B06-39", "Rare" },
				{ "B06-40", "Common" },
				{ "B06-41", "Rare" },
				{ "B06-42", "Uncommon" },
				{ "B06-43", "Common" },
				{ "B06-44", "Uncommon" },
				{ "B06-45", "Rare" },
				{ "B06-46", "Common" },
				{ "B06-47", "Uncommon" },
				{ "B06-48", "Rare" },
				{ "B06-49", "Common" },
				{ "B06-50", "Uncommon" },
				{ "B06-51", "Rare" },
				{ "B06-52", "Uncommon" },
				{ "B06-53", "Common" },
				{ "B06-54", "Rare" },
				{ "B06-55", "Uncommon" },
				{ "B06-56", "Common" },
				{ "B06-57", "Rare" },
				{ "B06-58", "Uncommon" },
				{ "B06-59", "Common" },
				{ "B06-60", "Rare" },
				{ "B06-61", "Ultra Rare" },
				{ "B06-62", "Uncommon" },
				{ "B06-63", "Uncommon" },
				{ "B06-64", "Uncommon" },
				{ "B06-65", "Common" },
				{ "B06-66", "Ultra Rare" },
				{ "B06-67", "Common" },
				{ "B06-68", "Common" },
				{ "B06-69", "Uncommon" },
				{ "B06-70", "Common" },
				{ "B06-71", "Common" },
				{ "B06-72", "Uncommon" },
				{ "B06-73", "Uncommon" },
				{ "B06-74", "Rare" },
				{ "B06-75", "Common" },
				{ "B06-76", "Common" },
				{ "B06-77", "Uncommon" },
				{ "B07-01", "Rare" },
				{ "B07-02", "Rare" },
				{ "B07-03", "Uncommon" },
				{ "B07-04", "Common" },
				{ "B07-05", "Uncommon" },
				{ "B07-06", "Rare" },
				{ "B07-07", "Ultra Rare" },
				{ "B07-08", "Ultra Rare" },
				{ "B07-09", "Rare" },
				{ "B07-10", "Rare" },
				{ "B07-11", "Common" },
				{ "B07-12", "Common" },
				{ "B07-13", "Common" },
				{ "B07-14", "Rare" },
				{ "B07-15", "Uncommon" },
				{ "B07-16", "Common" },
				{ "B07-17", "Uncommon" },
				{ "B07-18", "Uncommon" },
				{ "B07-19", "Uncommon" },
				{ "B07-20", "Common" },
				{ "B07-21", "Common" },
				{ "B07-22", "Common" },
				{ "B07-23", "Uncommon" },
				{ "B07-24", "Uncommon" },
				{ "B07-25", "Common" },
				{ "B07-26", "Rare" },
				{ "B07-27", "Uncommon" },
				{ "B07-28", "Ultra Rare" },
				{ "B07-29", "Rare" },
				{ "B07-30", "Uncommon" },
				{ "B07-31", "Rare" },
				{ "B07-32", "Common" },
				{ "B07-33", "Common" },
				{ "B07-34", "Uncommon" },
				{ "B07-35", "Rare" },
				{ "B07-36", "Common" },
				{ "B07-37", "Uncommon" },
				{ "B07-38", "Rare" },
				{ "B07-39", "Uncommon" },
				{ "B07-40", "Common" },
				{ "B07-41", "Rare" },
				{ "B07-42", "Rare" },
				{ "B07-43", "Common" },
				{ "B07-44", "Rare" },
				{ "B07-45", "Uncommon" },
				{ "B07-46", "Uncommon" },
				{ "B07-47", "Uncommon" },
				{ "B07-48", "Common" },
				{ "B07-49", "Common" },
				{ "B07-50", "Common" },
				{ "B07-51", "Rare" },
				{ "B07-52", "Ultra Rare" },
				{ "B07-53", "Common" },
				{ "B07-54", "Common" },
				{ "B07-55", "Rare" },
				{ "B07-56", "Rare" },
				{ "B07-57", "Uncommon" },
				{ "B07-58", "Rare" },
				{ "B07-59", "Common" },
				{ "B07-60", "Uncommon" },
				{ "B07-61", "Rare" },
				{ "B07-62", "Ultra Rare" },
				{ "B07-63", "Uncommon" },
				{ "B07-64", "Common" },
				{ "B07-65", "Common" },
				{ "B07-66", "Common" },
				{ "B07-67", "Common" },
				{ "B07-68", "Common" },
				{ "B07-69", "Common" },
				{ "B07-70", "Common" },
				{ "B07-71", "Common" },
				{ "B07-72", "Common" },
				{ "B07-73", "Rare" },
				{ "B07-74", "Common" },
				{ "B07-75", "Uncommon" },
				{ "B08-01", "Common" },
				{ "B08-02", "Common" },
				{ "B08-03", "Rare" },
				{ "B08-04", "Rare" },
				{ "B08-05", "Uncommon" },
				{ "B08-06", "Rare" },
				{ "B08-07", "Uncommon" },
				{ "B08-08", "Uncommon" },
				{ "B08-09", "Ultra Rare" },
				{ "B08-10", "Rare" },
				{ "B08-11", "Common" },
				{ "B08-12", "Common" },
				{ "B08-13", "Common" },
				{ "B08-14", "Common" },
				{ "B08-15", "Common" },
				{ "B08-16", "Uncommon" },
				{ "B08-17", "Common" },
				{ "B08-18", "Common" },
				{ "B08-19", "Common" },
				{ "B08-20", "Common" },
				{ "B08-21", "Common" },
				{ "B08-22", "Uncommon" },
				{ "B08-23", "Uncommon" },
				{ "B08-24", "Uncommon" },
				{ "B08-25", "Uncommon" },
				{ "B08-26", "Uncommon" },
				{ "B08-27", "Ultra Rare" },
				{ "B08-28", "Ultra Rare" },
				{ "B08-29", "Rare" },
				{ "B08-30", "Rare" },
				{ "B08-31", "Common" },
				{ "B08-32", "Uncommon" },
				{ "B08-33", "Rare" },
				{ "B08-34", "Common" },
				{ "B08-35", "Uncommon" },
				{ "B08-36", "Ultra Rare" },
				{ "B08-37", "Rare" },
				{ "B08-38", "Rare" },
				{ "B08-39", "Common" },
				{ "B08-40", "Rare" },
				{ "B08-41", "Rare" },
				{ "B08-42", "Common" },
				{ "B08-43", "Uncommon" },
				{ "B08-44", "Rare" },
				{ "B08-45", "Common" },
				{ "B08-46", "Common" },
				{ "B08-47", "Ultra Rare" },
				{ "B08-48", "Uncommon" },
				{ "B08-49", "Common" },
				{ "B08-50", "Common" },
				{ "B08-51", "Uncommon" },
				{ "B08-52", "Uncommon" },
				{ "B08-53", "Common" },
				{ "B08-54", "Rare" },
				{ "B08-55", "Common" },
				{ "B08-56", "Common" },
				{ "B08-57", "Rare" },
				{ "B08-58", "Common" },
				{ "B08-59", "Uncommon" },
				{ "B08-60", "Rare" },
				{ "B08-61", "Rare" },
				{ "B08-62", "Uncommon" },
				{ "B08-63", "Common" },
				{ "B08-64", "Rare" },
				{ "B08-65", "Uncommon" },
				{ "B08-66", "Rare" },
				{ "B08-67", "Common" },
				{ "B08-68", "Common" },
				{ "B08-69", "Common" },
				{ "B08-70", "Common" },
				{ "B08-71", "Rare" },
				{ "B08-72", "Uncommon" },
				{ "B08-73", "Common" },
				{ "B08-74", "Common" },
				{ "B08-75", "Uncommon" }
			};
		private static readonly List<string[]> _translated = [.. Encoding.UTF8.GetString(CardResources.mhhcdata2)!.Split("\r\n").Select(x => x.Split("\t"))];
		private static readonly IMemoryCache _cache = Utilities.GetService<IMemoryCache>();

		public static void TryAddCardToCache(Guid cardGuid, MHHCardPackage package)
		{
			_cache.GetOrCreate(cardGuid, x =>
			{
				x.AbsoluteExpirationRelativeToNow = new TimeSpan(24, 0, 0);
				return package;
			});
		}

		private static CardRarity RarityFromString(string rarityName)
		{
			return new Dictionary<string, CardRarity>()
			{
				{  "Common", CardRarity.Common },
				{  "Uncommon", CardRarity.Uncommon },
				{  "Rare", CardRarity.Rare },
				{  "Ultra Rare", CardRarity.Ultra },
			}[rarityName];
		}

		public static async Task<CardDeco> RollDeco(int mult = 1, bool forceRare = false)
		{
			MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables();
			int maxHoloChance = 50;
			if (var.CurrentEvent == Events.DoubleHolo)
			{
				maxHoloChance /= 2;
			}
			maxHoloChance /= mult;
			int maxSpecialChance = 25;
			if (var.CurrentEvent == Events.DoubleSpecial)
			{
				maxSpecialChance /= 2;
			}
			maxSpecialChance /= mult;
			int maxRareChance = 10;
			if (var.CurrentEvent == Events.DoubleRare)
			{
				maxRareChance /= 2;
			}
			maxRareChance /= mult;
			if (_rand.Next(1, maxHoloChance + 1) == 1)
			{
				return CardDeco.Holo;
			}
			else if (_rand.Next(1, maxSpecialChance + 1) == 1 || forceRare)
			{
				return (CardDeco)_rand.Next(6 + ((int)var.CurrentSpecialEdition * 6), 12 + ((int)var.CurrentSpecialEdition * 6));
			}
			else if (_rand.Next(1, maxRareChance + 1) == 1)
			{
				return (CardDeco)_rand.Next(2, 5);
			}
			else
			{
				return CardDeco.Normal;
			}
		}

		public static int RollCardId(int series, BoosterRarity? boosterRarity = null)
		{
			string[] rarityNames = ["Common", "Uncommon", "Rare", "Ultra Rare"];
			CardRarity rarity = CardRarity.Common;
			if (boosterRarity != null)
			{
				switch (boosterRarity)
				{
					case BoosterRarity.Common:
						rarity = CardRarity.Common;
						break;
					case BoosterRarity.Uncommon:
						switch (_rand.Next(1, 4))
						{
							case 1:
								rarity = CardRarity.Common;
								break;
							case 2:
								rarity = CardRarity.Uncommon;
								break;
							case 3:
								rarity = CardRarity.Rare;
								break;
						}
						break;
					case BoosterRarity.Foil:
						switch (_rand.Next(1, 3))
						{
							case 1:
								rarity = CardRarity.Rare;
								break;
							case 2:
								rarity = CardRarity.Ultra;
								break;
						}
						break;
				}
			}
			else
			{
				switch (_rand.Next(1, 101))
				{
					case <= 50:
						rarity = CardRarity.Common;
						break;
					case <= 80:
						rarity = CardRarity.Uncommon;
						break;
					case <= 95:
						rarity = CardRarity.Rare;
						break;
					case <= 100:
						rarity = CardRarity.Ultra;
						break;
				}
			}
			string[] ids = [.. _cardMaps.Where(x => x.Key.StartsWith($"B{series:00}") && x.Value == rarityNames[(int)rarity]).Select(x => x.Key)];
			string key = ids[_rand.Next(0, ids.Length)];
			return Convert.ToInt32(key[(key.IndexOf('-') + 1)..]);
		}

		public static async Task<MHHCardPackage> BuildCardPackage(MHHCard card)
		{
			int[] ids = [.. card.CardId.Split("-").Select(x => Convert.ToInt32(x.Replace("B0", "")))];
			return await BuildCardPackage(true, ids[0], ids[1], card.Decoration, card.Guid);
		}

		public static async Task<MHHCardPackage> BuildCardPackage(int series, CardRarity? cardRarity = null, CardDeco? cardDeco = null)
		{
			if (cardRarity != null)
			{
				string[] rarityNames = ["Common", "Uncommon", "Rare", "Ultra Rare"];
				int rarity = (int)cardRarity + 1;
				string[] ids = [.. _cardMaps.Where(x => x.Key.StartsWith($"B{series:00}") && x.Value == rarityNames[rarity]).Select(x => x.Key)];
				string key = ids[_rand.Next(0, ids.Length)];
				int[] keyIds = [.. key.Split("-").Select(x => Convert.ToInt32(x.Replace("B0", "")))];
				return await BuildCardPackage(true, keyIds[0], keyIds[1]);
			}
			else
			{
				MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables();
				if (cardDeco < CardDeco.Trophy && cardDeco > CardDeco.Holo)
				{
					cardDeco = (CardDeco)_rand.Next(6 + ((int)var.CurrentSpecialEdition * 6), 12 + ((int)var.CurrentSpecialEdition * 6));
				}
				else if (cardDeco > CardDeco.Trophy && cardDeco != CardDeco.Holo)
				{
					cardDeco = CardDeco.Holo;
				}
				else
				{
					cardDeco = (CardDeco)_rand.Next(2, 5);
				}
				return await BuildCardPackage(true, series, RollCardId(series), cardDeco);
			}
		}

		public static async Task<MHHCardPackage> BuildCardPackage(bool isBaseCard, int series, int cardNo, CardDeco? cardDeco = null, Guid? cardGuid = null, BoosterRarity? boosterRarity = null)
		{
			cardGuid ??= Guid.NewGuid();
			MHHCardPackage? ret = null;
			try
			{
				if (cardGuid != null)
				{
					_cache.TryGetValue(cardGuid, out ret);
				}
				if (ret == null)
				{
					string cardId = $"B{series:00}-{cardNo:00}";
					CardRarity cardRarity = RarityFromString(_cardMaps[cardId]);
					int mult = 1;
					bool forceRare = boosterRarity != null && ((boosterRarity == BoosterRarity.Uncommon && cardRarity == CardRarity.Common) || (boosterRarity == BoosterRarity.Foil && cardRarity == CardRarity.Rare));
					cardDeco ??= boosterRarity == BoosterRarity.Common ? CardDeco.Normal : await RollDeco(mult, forceRare: forceRare);
					byte[]? srcCardBytes = null;
					string cardPath = Path.Combine(Utilities.CardStoragePath, series.ToString(), cardNo + "_" + cardDeco.GetDescription() + ".json");
					if (File.Exists(cardPath))
					{
						srcCardBytes = JsonConvert.DeserializeObject<MHHCardStorage>(File.ReadAllText(cardPath))!.StoredCard;
					}
					else
					{
						using (MemoryStream finalStream = new())
						{
							//Outdated database storage
							//using (Wiki_DbContext ctxt = new())
							//{
							//	MHHCardStorage? storedCard = await ctxt.MHHCardStorage.FirstOrDefaultAsync(x => x.Series == series && x.CardNumber == cardNo && x.Decoration == cardDeco);
							//	srcCardBytes = storedCard?.StoredCard;
							//	if (srcCardBytes != null)
							//	{
							//		finalStream = new(srcCardBytes!);
							//	}
							//}
							if (srcCardBytes == null)
							{
								string cardUri = $"https://raw.githubusercontent.com/GrenderG/MHHC_Archive/94f6feca23fd88ce90418168e36d591c5e174b31/Card%20Scans/{(isBaseCard ? "Base" : "Starter")}%20{series:00}/{(isBaseCard ? "B" : "S")}{series:00}-{cardNo:00}.png";
								if (!_cache.TryGetValue(cardUri, out srcCardBytes))
								{
									using (HttpClient client = new())
									{
										HttpResponseMessage response = await client.GetAsync(cardUri);
										srcCardBytes = await response.Content.ReadAsByteArrayAsync();
										ICacheEntry entry = _cache.CreateEntry(cardUri);
										entry.AbsoluteExpirationRelativeToNow = new TimeSpan(24, 0, 0);
										entry.Value = srcCardBytes;
									}
								}
								byte[] newBytes = [];
								using (MemoryStream ms = new(srcCardBytes!))
								using (Image baseImage = Image.Load(ms))
								{
									baseImage.Mutate(x => x.Resize(2858, 4086));
									baseImage.Mutate(x =>
										x.ApplyCardEffects(cardDeco.Value, baseImage, _rand)
										.EntropyCrop()
										.RoundCorners(100)
										.Resize(new ResizeOptions
										{
											Size = new Size(x.GetCurrentSize().Width - 10, x.GetCurrentSize().Height - 10),
											Mode = ResizeMode.Crop
										})
										.Resize(baseImage.Width / 2, baseImage.Height / 2)
									);
									baseImage.SaveAsPng(finalStream);
									newBytes = finalStream.ToArray();
								}
								srcCardBytes = newBytes;
							}
						}
					}
					string deco = cardDeco.Value == CardDeco.Normal ? "" : " - " + cardDeco.Value.GetDescription();
					string[] row = _translated.First(x => x[0] == series.ToString() && x[2] == cardNo.ToString());
					ret = new MHHCardPackage()
					{
						Card = new()
						{
							Guid = cardGuid!.Value,
							CardName = row[5].Replace("{{{LINEBREAK}}}", "\r\n"),
							CardNameJP = row[4].Replace("{{{LINEBREAK}}}", "\r\n"),
							CardType = row[3].Replace("{{{LINEBREAK}}}", "\r\n"),
							CardDescription = row[22].Replace("{{{LINEBREAK}}}", "\r\n").Replace("\"", ""),
							CardId = cardId,
							Power = row[6].Replace("{{{LINEBREAK}}}", "\r\n"),
							Rank = row[7].Replace("{{{LINEBREAK}}}", "\r\n"),
							HunterWeapon = row[8].Replace("{{{LINEBREAK}}}", "\r\n"),
							HunterArmor = row[19].Replace("{{{LINEBREAK}}}", "\r\n"),
							Rarity = cardRarity,
							Decoration = cardDeco.Value
						},
						CardBytes = srcCardBytes
					};
					//using (Wiki_DbContext ctxt = new())
					//{
					//if (!ctxt.MHHCardStorage.Any(x => x.Series == series && x.CardNumber == cardNo && x.Decoration == cardDeco!))
					//{
					//	await ctxt.MHHCardStorage.AddAsync(new()
					//	{
					//		CardNumber = cardNo,
					//		Decoration = cardDeco.Value,
					//		Series = series,
					//		StoredCard = srcCardBytes!
					//	});
					//	await ctxt.SaveChangesAsync();
					//}
					//}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
			return ret!;
		}
	}

	static class IImageProcessingContextExtensions
	{
		public static IImageProcessingContext ApplyCardEffects(this IImageProcessingContext ctxt, CardDeco choice, Image baseImage, Random rand)
		{
			switch (choice)
			{
				case CardDeco.Normal: break;
				case CardDeco.Holo:
					{
						using (Image holoBmp = rand.Next(1, 5) switch
						{
							1 => Image.Load(CardResources.holo1),
							2 => Image.Load(CardResources.holo2),
							3 => Image.Load(CardResources.holo3),
							4 => Image.Load(CardResources.holo4),
							_ => Image.Load(CardResources.holo1),
						})
						{
							holoBmp.Mutate(x => x.Resize(baseImage.Width, baseImage.Height));
							baseImage.Mutate(x => x.DrawImage(holoBmp, .95f));
						}
					}
					break;
				case CardDeco.Negative:
					{
						baseImage.Mutate(x => x.ApplyProcessor(new SixLabors.ImageSharp.Processing.Processors.Filters.InvertProcessor(1f), new Rectangle(28, 475, 2782, 2500)).Saturate(.5f));
					}
					break;
				case CardDeco.Grayscale:
					{
						baseImage.Mutate(x => x.Grayscale(new Rectangle(28, 475, 2782, 2500)));
					}
					break;
				case CardDeco.Sepia:
					{
						baseImage.Mutate(x => x.Sepia(new Rectangle(28, 475, 2782, 2500)));
					}
					break;
				case CardDeco.Trophy:
					{
						using (Image originalBase = baseImage.Clone(x => { }))
						using (Image yellowBase = baseImage.Clone(x => { }))
						using (Image goldFoil = Image.Load(CardResources.goldfoil))
						using (Image liquidGold = Image.Load(CardResources.liquidgold2))
						using (Image crown = Image.Load(CardResources.crown))
						{
							liquidGold.Mutate(x => x.Resize(baseImage.Width, baseImage.Height));
							goldFoil.Mutate(x => x.Resize(baseImage.Width, baseImage.Height));
							baseImage.Mutate(x =>
							{
								IImageProcessingContext ctxt = x.DrawImage(goldFoil, 1f)
									//was .9f
									.DrawImage(liquidGold, .80f);
								yellowBase.Mutate(x => x.Invert().Saturate(.8f).Sepia().Saturate(10));
								ctxt = ctxt.DrawImage(yellowBase, .35f)
									.DrawImage(crown, backgroundLocation: new Point(1069, 3190), .3f)
									.DrawImage(originalBase, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f);
							}
							);
						}
					}
					break;
				case CardDeco.Iron:
				case CardDeco.Dreamcore:
				case CardDeco.Artian:
				case CardDeco.Aquacore:
				case CardDeco.Eltalite:
				case CardDeco.Dragoncore:
					{
						using (Image originalBase = baseImage.Clone(x => { }))
						using (Image coloredBase = baseImage.Clone(x => { }))
						using (Image goldFoil = Image.Load(CardResources.goldfoil))
						using (Image liquidGold = Image.Load(CardResources.liquidgold2))
						{
							liquidGold.Mutate(x => x.Resize(baseImage.Width, baseImage.Height).Grayscale().Saturate(2));
							goldFoil.Mutate(x => x.Resize(baseImage.Width, baseImage.Height).Grayscale().Saturate(2));
							goldFoil.Mutate(x => x.Opacity(.5f).DrawImage(liquidGold, 0.5f).Opacity(1f));
							Color color = Color.Transparent;
							bool recolor = true;
							switch (choice)
							{
								case CardDeco.Iron:
									recolor = false;
									break;
								case CardDeco.Dreamcore:
									color = Color.SaddleBrown;
									break;
								case CardDeco.Artian:
									color = Color.DarkSeaGreen;
									break;
								case CardDeco.Aquacore:
									color = Color.SlateBlue;
									break;
								case CardDeco.Eltalite:
									color = Color.Orchid;
									break;
								case CardDeco.Dragoncore:
									color = Color.Purple;
									break;
							}

							if (recolor)
							{
								coloredBase.Mutate(y => y.Grayscale().Fill(new DrawingOptions()
								{
									GraphicsOptions = new GraphicsOptions()
									{
										ColorBlendingMode = PixelColorBlendingMode.Overlay,
										BlendPercentage = 1f
									}
								}, color).Invert());
								goldFoil.Mutate(y => y.Grayscale().Fill(new DrawingOptions()
								{
									GraphicsOptions = new GraphicsOptions()
									{
										ColorBlendingMode = PixelColorBlendingMode.Overlay,
										BlendPercentage = 1f
									}
								}, color));
								baseImage.Mutate(x =>
									x.Grayscale().Fill(new DrawingOptions()
									{
										GraphicsOptions = new GraphicsOptions()
										{
											ColorBlendingMode = PixelColorBlendingMode.Overlay,
											BlendPercentage = 1f
										}
									}, color)
									.DrawImage(coloredBase, 1f)
								);
							}
							baseImage.Mutate(x =>
								x.DrawImage(goldFoil, .75f));
							if (choice == CardDeco.Iron)
							{
								baseImage.Mutate(x => x.Grayscale()
									.Saturate(10)
									.Invert());
							}
							baseImage.Mutate(x => x.DrawImage(originalBase, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f));
						}
					}
					break;
				case CardDeco.Firecell:
				case CardDeco.Machalite:
				case CardDeco.Dragonite:
				case CardDeco.Icium:
				case CardDeco.Deepsea:
				case CardDeco.Shadowcore:
					using (Image originalBase = baseImage.Clone(x => { }))
					using (Image coloredBase = baseImage.Clone(x => { }))
					using (Image gem = Image.Load(CardResources.gemlarge))
					{
						Color color = Color.Transparent;
						switch (choice)
						{
							case CardDeco.Firecell:
								color = Color.FromRgb(30, 170, 180);
								break;
							case CardDeco.Machalite:
								color = Color.RebeccaPurple;
								break;
							case CardDeco.Dragonite:
								color = Color.SeaGreen;
								break;
							case CardDeco.Icium:
								color = Color.Red;
								break;
							case CardDeco.Deepsea:
								color = Color.FromRgb(255, 171, 0);
								break;
							case CardDeco.Shadowcore:
								color = Color.FromRgb(55, 130, 0);
								break;
						}
						gem.Mutate(x =>
							x.Resize(baseImage.Width, baseImage.Height)
							.Fill(new DrawingOptions()
							{
								GraphicsOptions = new GraphicsOptions()
								{
									ColorBlendingMode = PixelColorBlendingMode.Overlay,
									BlendPercentage = 1f
								}
							}, color)
							.Invert()
							.Saturate(2));
						coloredBase.Mutate(x => x.Grayscale().Fill(new DrawingOptions()
						{
							GraphicsOptions = new GraphicsOptions()
							{
								ColorBlendingMode = PixelColorBlendingMode.Overlay,
								BlendPercentage = 1f
							}
						}, color)
							.Saturate(2));
						baseImage.Mutate(x =>
						{
							x.DrawImage(coloredBase, 1f)
							.DrawImage(gem, .6f)
							.DrawImage(originalBase, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f);
						});
					}
					break;
				case CardDeco.Unused_NegativeTrophy:
					using (Image originalBase = baseImage.Clone(x => { }))
					using (Image yellowBase = baseImage.Clone(x => { }))
					using (Image goldFoil = Image.Load(CardResources.goldfoil))
					using (Image liquidGold = Image.Load(CardResources.liquidgold2))
					using (Image crown = Image.Load(CardResources.crown))
					{
						liquidGold.Mutate(x => x.Resize(baseImage.Width, baseImage.Height));
						goldFoil.Mutate(x => x.Resize(baseImage.Width, baseImage.Height));
						baseImage.Mutate(x =>
						{
							IImageProcessingContext ctxt = x.DrawImage(goldFoil, 1f)
								//was .9f
								.DrawImage(liquidGold, .80f);
							yellowBase.Mutate(x => x.Sepia().Saturate(10).Invert().Saturate(.8f));
							ctxt = ctxt.DrawImage(yellowBase, .35f)
								.DrawImage(crown, backgroundLocation: new Point(1069, 3190), .3f);
							ctxt.DrawImage(originalBase, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f);
						}
						);
					}
					break;
			}
			return ctxt;
		}
	}

	public enum BoosterRarity
	{
		Common,
		Uncommon,
		Foil
	}

	public enum CardRarity
	{
		[Description("Common")]
		Common,
		[Description("Uncommon")]
		Uncommon,
		[Description("Rare")]
		Rare,
		[Description("Ultra Rare")]
		Ultra
	}

	public enum CardDeco
	{
		[Description("Normal")]
		Normal,
		[Description("🌈 Holographic 🌈")]
		Holo,
		[Description("🔳 Negative 🔳")]
		Negative,
		[Description("🔲 Grayscale 🔲")]
		Grayscale,
		[Description("🌅 Sepia 🌅")]
		Sepia,
		[Description("🏆 Trophy 🏆")]
		Trophy,
		//Silver
		[Description("⚙️ Iron ⚙️")]
		Iron,
		//Bronze
		[Description("⚙️ Dreamcore ⚙️")]
		Dreamcore,
		//Pale green/burnished copper
		[Description("⚙️ Artian ⚙️")]
		Artian,
		//Ocean blue
		[Description("⚙️ Aquacore ⚙️")]
		Aquacore,
		//Pink or red
		[Description("⚙️ Eltalite ⚙️")]
		Eltalite,
		//Purple
		[Description("⚙️ Dragoncore ⚙️")]
		Dragoncore,
		//Orange
		[Description("💎 Firecell 💎")]
		Firecell,
		//Green
		[Description("💎 Machalite 💎")]
		Machalite,
		//Pink
		[Description("💎 Dragonite 💎")]
		Dragonite,
		//Pale blue
		[Description("💎 Icium 💎")]
		Icium,
		//Deep blue
		[Description("💎 Deepsea 💎")]
		Deepsea,
		//Purple
		[Description("💎 Shadowcore 💎")]
		Shadowcore,
		[Description("(Unused) Negative Trophy")]
		Unused_NegativeTrophy
	}
}
