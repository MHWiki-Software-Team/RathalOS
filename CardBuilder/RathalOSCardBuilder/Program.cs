using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RathalOS;
using RathalOS.Data.Context;
using RathalOS.Data.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;

namespace RathalOSCardBuilder
{
    internal class Program
	{
		private static Random _rand { get; set; } = new();
		private static IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
		static void Main(string[] args)
		{
			int cntr = 1;
			for (CardDeco deco = CardDeco.Herb; deco <= CardDeco.AdamantSeed; deco++)
			{
				MHHCardPackage pkg = StoreCard(deco, 73, 1, true).Result;
				File.WriteAllBytes(@"C:\Users\mkast\OneDrive\Desktop\test" + cntr + ".png", pkg.CardBytes);
				cntr++;
			}
			Console.WriteLine("Written");
			bool generateAll = false;
			if (generateAll)
			{
				List<Task> allCards = new List<Task>();
				using (Wiki_DbContext ctxt = new())
				{
					//if you want the db overhead, you could always do this
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
					Task.Run(async () =>
					{
						for (int series = 1; series <= 8; series++)
						{
							int[] seriesCnts = [];
							//starters have a weird border and no rarities
							bool isBase = true;// _rand.Next(0, 2) == 0;
							if (isBase)
							{
								seriesCnts = [90, 77, 77, 77, 90, 77, 75, 75];
							}
							else
							{
								seriesCnts = [17, 24, 22, 3, 27, 28, 24, 26];
							}
							List<MHHCardStorage> storageCardsForSeries = [];
							for (int cardNumber = 1; cardNumber <= seriesCnts[series - 1]; cardNumber++)
							{
								foreach (CardDeco deco in Enum.GetValues(typeof(CardDeco)))
								{
									//Database card storage for if the app is containerized (not recommended)
									//if (!ctxt.MHHCardStorage.Any(x => x.Series == series && x.CardNumber == cardNumber && x.Decoration == deco))
									//{
									//	MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(true, series, cardNumber, deco);
									//	storageCardsForSeries.Add(new MHHCardStorage()
									//	{
									//		CardNumber = cardNumber,
									//		Series = series,
									//		Decoration = deco,
									//		StoredCard = pkg.CardBytes
									//	});
									//}
									StoreCard(deco, cardNumber, series);
									Console.WriteLine($"Wrote B{series:D2} {cardNumber}/{seriesCnts[series - 1]} - {deco.GetDescription()}");
								}
							}
						}
					});
				}
			}
		}

		static async Task<MHHCardPackage> StoreCard(CardDeco deco, int cardNumber, int series, bool forceStore = false)
		{
			string seriesPath = System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cards"), series.ToString());
			if (!Directory.Exists(seriesPath))
			{
				Directory.CreateDirectory(seriesPath);
			}
			string cardPath = System.IO.Path.Combine(seriesPath, cardNumber + "_" + deco.GetDescription() + ".json");
			MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(true, series, cardNumber, deco);
			if (!File.Exists(cardPath) || forceStore)
			{
				pkg.CardBytes = await PaintCard(pkg, series, cardNumber);
				File.WriteAllText(cardPath, JsonConvert.SerializeObject(new MHHCardStorage()
				{
					CardNumber = cardNumber,
					Series = series,
					Decoration = deco,
					StoredCard = pkg.CardBytes
				}));
			}
			return pkg;
		}

		static async Task<byte[]> PaintCard(MHHCardPackage pkg, int series, int cardNumber)
		{
			byte[]? srcCardBytes = [];
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
				bool isBaseCard = true;
				string cardUri = $"https://raw.githubusercontent.com/GrenderG/MHHC_Archive/master/Card%20Scans/{(isBaseCard ? "Base" : "Starter")}%20{series:00}/{(isBaseCard ? "B" : "S")}{series:00}-{cardNumber:00}.png";
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
						x.ApplyCardEffects(pkg.Card.Decoration, baseImage, _rand)
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
			return srcCardBytes!;
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
				case CardDeco choiceTemp when choiceTemp >= CardDeco.Iron && choiceTemp <= CardDeco.Dragoncore:
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
				case CardDeco choiceTemp when choiceTemp >= CardDeco.Firecell && choiceTemp <= CardDeco.Shadowcore:
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
				case CardDeco choiceTemp when choiceTemp >= CardDeco.GreatSword && choiceTemp <= CardDeco.Bow:
					byte[] overlay = new byte[0];
					switch (choice)
					{
						case CardDeco.GreatSword:
							overlay = CardResources.GSOverlay;
							break;
						case CardDeco.LongSword:
							overlay = CardResources.LSOverlay;
							break;
						case CardDeco.SwordAndShield:
							overlay = CardResources.SNSOverlay;
							break;
						case CardDeco.Hammer:
							overlay = CardResources.HmOverlay;
							break;
						case CardDeco.HuntingHorn:
							overlay = CardResources.HHOverlay;
							break;
						case CardDeco.ChargeBlade:
							overlay = CardResources.CBOverlay;
							break;
						case CardDeco.Gunlance:
							overlay = CardResources.GLOverlay;
							break;
						case CardDeco.InsectGlaive:
							overlay = CardResources.IGOverlay;
							break;
						case CardDeco.Bow:
							overlay = CardResources.BoOverlay;
							break;
						case CardDeco.HeavyBowgun:
							overlay = CardResources.HBGOverlay;
							break;
						case CardDeco.LightBowgun:
							overlay = CardResources.LBGOverlay;
							break;
						case CardDeco.SwitchAxe:
							overlay = CardResources.SAOverlay;
							break;
						case CardDeco.Lance:
							overlay = CardResources.LNOverlay;
							break;
						case CardDeco.DualBlades:
							overlay = CardResources.DBOverlay;
							break;
					}
					using (Image originalBase = baseImage.Clone(x => { }))
					using (Image redSilk = Image.Load(CardResources.redsilk))
					using (Image weaponOverlay = Image.Load(overlay))
					{
						redSilk.Mutate(x => x.Saturate(.8f));
						redSilk.Mutate(x => x.Resize(baseImage.Width, baseImage.Height));
						weaponOverlay.Mutate(x =>x.Resize(baseImage.Width, baseImage.Height));
						baseImage.Mutate(x =>
						{
							x.DrawImage(redSilk, .45f)
							.DrawImage(originalBase, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f)
							.DrawImage(weaponOverlay, .5f);
						});
					}
					break;
				case CardDeco choiceTemp when choiceTemp >= CardDeco.Herb && choiceTemp <= CardDeco.AdamantSeed:
					{
						Color color;
						switch (choice)
						{
							case CardDeco.Herb:
								color = Color.ForestGreen;
								break;
							case CardDeco.AntidoteHerb:
								color = Color.RebeccaPurple;
								break;
							case CardDeco.HotPepper:
								color = Color.OrangeRed;
								break;
							case CardDeco.SnowHerb:
								color = Color.WhiteSmoke;
								break;
							case CardDeco.Nulberry:
								color = Color.CornflowerBlue;
								break;
							case CardDeco.MightSeed:
								color = Color.IndianRed;
								break;
							case CardDeco.AdamantSeed:
								color = Color.SaddleBrown;
								break;
							default: color = Color.ForestGreen; break;
						}
						using (Image originalBase = baseImage.Clone(x => { }))
						using (Image leafOverlay = Image.Load(CardResources.LeafOverlay1))
						using (Image herbOverlay = Image.Load(CardResources.HerbOverlay))
						{
							herbOverlay.Mutate(x => x.Grayscale()
								.Saturate(.5f));
							leafOverlay.Mutate(x => x.Grayscale()
								.Saturate(.8f)
								.Fill(new DrawingOptions()
								{
									GraphicsOptions = new GraphicsOptions()
									{
										ColorBlendingMode = PixelColorBlendingMode.Overlay,
										BlendPercentage = 1f
									}
								}, color)
								.Saturate(2));
							leafOverlay.Mutate(x => x.Resize(baseImage.Width, baseImage.Height));
							herbOverlay.Mutate(x => x.Fill(new DrawingOptions()
							{
								GraphicsOptions = new GraphicsOptions()
								{
									ColorBlendingMode = PixelColorBlendingMode.Add,
									BlendPercentage = .85f,
									AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
								}
							}, color).Saturate(.65f).Resize(baseImage.Width, baseImage.Height));
							baseImage.Mutate(x =>
							{
								x.Grayscale()
								.Saturate(.8f)
								.Fill(new DrawingOptions()
								{
									GraphicsOptions = new GraphicsOptions()
									{
										ColorBlendingMode = PixelColorBlendingMode.Overlay,
										BlendPercentage = 1f
									}
								}, color)
								.DrawImage(leafOverlay, .45f)
								.DrawImage(originalBase, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f)
								.DrawImage(herbOverlay, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f);
							});
						}
					}
					break;
				case CardDeco choiceTemp when choiceTemp >= CardDeco.BlueMushroom && choiceTemp <= CardDeco.Chillshroom:
					{
						Color color;
						switch (choice)
						{
							case CardDeco.BlueMushroom:
								color = Color.Blue;
								break;
							case CardDeco.Mandragora:
								color = Color.SandyBrown;
								break;
							case CardDeco.Nitroshroom:
								color = Color.IndianRed;
								break;
							case CardDeco.DevilsBlight:
								color = Color.DarkRed;
								break;
							case CardDeco.Parashroom:
								color = Color.Yellow;
								break;
							case CardDeco.Toadstool:
								color = Color.RebeccaPurple;
								break;
							case CardDeco.Exciteshroom:
								color = Color.SpringGreen;
								break;
							case CardDeco.Chillshroom:
								color = Color.WhiteSmoke;
								break;
							default: color = Color.CadetBlue; break;
						}
						using (Image originalBase = baseImage.Clone(x => { }))
						using (Image mushroomOverlay = Image.Load(CardResources.MushroomOverlay))
						using (Image mushroomOverlay2 = Image.Load(CardResources.MushroomOverlay2))
						{
							mushroomOverlay2.Mutate(x => x.Grayscale()
								.Saturate(.5f));
							mushroomOverlay.Mutate(x => x.Resize(baseImage.Width, baseImage.Height)
								.Grayscale()
								.Saturate(.8f)
								.Fill(new DrawingOptions()
								{
									GraphicsOptions = new GraphicsOptions()
									{
										ColorBlendingMode = PixelColorBlendingMode.Overlay,
										BlendPercentage = 1f
									}
								}, color)
								.Saturate(2));
							mushroomOverlay2.Mutate(x => x.Fill(new DrawingOptions()
								{
									GraphicsOptions = new GraphicsOptions()
									{
										ColorBlendingMode = PixelColorBlendingMode.Add,
										BlendPercentage = .85f,
										AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
									}
								}, color).Saturate(.65f).Resize(baseImage.Width, baseImage.Height));
							baseImage.Mutate(x =>
							{
								x.Grayscale()
								.Saturate(.8f)
								.Fill(new DrawingOptions()
								{
									GraphicsOptions = new GraphicsOptions()
									{
										ColorBlendingMode = PixelColorBlendingMode.Overlay,
										BlendPercentage = 1f
									}
								}, color)
								.DrawImage(mushroomOverlay, .45f)
								.DrawImage(originalBase, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f)
								.DrawImage(mushroomOverlay2, backgroundLocation: new Point(28, 475), foregroundRectangle: new Rectangle(28, 475, 2782, 2500), 1f);
							});
						}
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

	public static class IPathExtensions
	{
		private static IImageProcessingContext ApplyRoundedCorners(this IImageProcessingContext ctxt, float cornerRadius)
		{
			PathCollection corners = BuildCorners(ctxt.GetCurrentSize(), cornerRadius);
			return ctxt.Fill(new DrawingOptions() { GraphicsOptions = new GraphicsOptions() { Antialias = true } }, SixLabors.ImageSharp.Color.Transparent, corners);
		}

		private static PathCollection BuildCorners(Size size, float cornerRadius)
		{
			var rect = new RectangularPolygon(-0.5f, -0.5f, cornerRadius, cornerRadius);

			IPath cornerToptLeft = rect.Clip(new EllipsePolygon(cornerRadius - 0.5f, cornerRadius - 0.5f, cornerRadius));

			float rightPos = size.Width - cornerToptLeft.Bounds.Width + 1;
			float bottomPos = size.Height - cornerToptLeft.Bounds.Height + 1;

			IPath cornerTopRight = cornerToptLeft.RotateDegree(90).Translate(rightPos, 0);
			IPath cornerBottomLeft = cornerToptLeft.RotateDegree(-90).Translate(0, bottomPos);
			IPath cornerBottomRight = cornerToptLeft.RotateDegree(180).Translate(rightPos, bottomPos);

			return new PathCollection(cornerToptLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
		}

		public static IImageProcessingContext RoundCorners(this IImageProcessingContext processingContext, float cornerRadius)
		{
			return processingContext.Resize(new ResizeOptions
			{
				Size = processingContext.GetCurrentSize(),
				Mode = ResizeMode.Crop
			}).ApplyRoundedCorners(cornerRadius);
		}
	}
}
