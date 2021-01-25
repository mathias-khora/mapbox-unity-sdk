using System;
using Mapbox.Map;
using Mapbox.Platform;
using Mapbox.Unity;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Factories.TerrainStrategies;
using UnityEngine;

namespace CustomImageLayerSample
{
	public sealed class MapboxTerrainFactoryManager : ImageFactoryManager
	{
		public TerrainStrategy TerrainStrategy;
		public string ShaderElevationTextureFieldName = "_HeightTexture";
		public string ShaderElevationTextureScaleOffsetFieldName = "_HeightTexture_ST";

		private ElevationLayerProperties _elevationSettings;
		private bool _isUsingShaderSolution = true;

		public MapboxTerrainFactoryManager(
			IFileSource fileSource,
			ElevationLayerProperties elevationSettings,
			TerrainStrategy terrainStrategy,
			bool downloadFallbackImagery) : base(fileSource, elevationSettings.sourceOptions, downloadFallbackImagery)
		{
			_elevationSettings = elevationSettings;
			TerrainStrategy = terrainStrategy;
			_isUsingShaderSolution = !_elevationSettings.colliderOptions.addCollider;
		}

		public override void RegisterTile(UnityTile tile)
		{
			if (TerrainStrategy is IElevationBasedTerrainStrategy)
			{
				if (_isUsingShaderSolution)
				{
					ApplyParentTexture(tile);
				}

				var dataTile = CreateTile(tile.CanonicalTileId, _sourceSettings.Id);
				if (tile != null)
				{
					tile.AddTile(dataTile);
				}

				_fetcher.FetchData(dataTile, _sourceSettings.Id, tile.CanonicalTileId, tile);
			}
			else
			{
				//reseting height data
				tile.SetHeightData( null);
				TerrainStrategy.RegisterTile(tile, false);
			}
		}

		public override void UnregisterTile(UnityTile tile)
		{
			base.UnregisterTile(tile);
			TerrainStrategy.UnregisterTile(tile);
		}

		protected override RasterTile CreateTile(CanonicalTileId tileId, string tilesetId)
		{
			RasterTile rasterTile;

			if (tilesetId.StartsWith("mapbox://", StringComparison.Ordinal))
			{
				rasterTile = new DemTile(tileId, tilesetId);
			}
			else
			{
				rasterTile = new RawPngRasterTile(tileId, tilesetId);
			}

#if UNITY_EDITOR
			rasterTile.IsMapboxTile = true;
#endif

			return rasterTile;
		}

		protected override void SetTexture(UnityTile unityTile, RasterTile dataTile)
		{
			var cachedTileIdForCallbackCheck = unityTile.CanonicalTileId;
			if (dataTile.Texture2D != null)
			{
				//if collider is disabled, we switch to a shader based solution
				//no elevated mesh is generated
				if (_isUsingShaderSolution)
				{
					unityTile.MeshRenderer.sharedMaterial.SetTexture(ShaderElevationTextureFieldName, dataTile.Texture2D);
					unityTile.MeshRenderer.sharedMaterial.SetVector(ShaderElevationTextureScaleOffsetFieldName, new Vector4(1, 1, 0, 0));
					unityTile.MeshRenderer.sharedMaterial.SetFloat("_TileScale", unityTile.TileScale);
					unityTile.SetHeightData(dataTile, _elevationSettings.requiredOptions.exaggerationFactor, _elevationSettings.modificationOptions.useRelativeHeight, _elevationSettings.colliderOptions.addCollider);
					TerrainStrategy.RegisterTile(unityTile, false);
				}
				else
				{
					unityTile.SetHeightData(dataTile, _elevationSettings.requiredOptions.exaggerationFactor, _elevationSettings.modificationOptions.useRelativeHeight, _elevationSettings.colliderOptions.addCollider, (tile) =>
					{
						TerrainStrategy.RegisterTile(unityTile, true);
					});
				}
			}
			else
			{
				unityTile.SetHeightData(dataTile, _elevationSettings.requiredOptions.exaggerationFactor, _elevationSettings.modificationOptions.useRelativeHeight, _elevationSettings.colliderOptions.addCollider, (tile) =>
				{
					if (tile.CanonicalTileId == cachedTileIdForCallbackCheck)
					{
						TerrainStrategy.RegisterTile(unityTile, true);
					}
				});

			}
		}

		public void PregenerateTileMesh(UnityTile tile)
		{
			//TerrainStrategy.RegisterTile(tile, false);
		}

		protected override void ApplyParentTexture(UnityTile tile)
		{
			var parent = tile.UnwrappedTileId.Parent;
			for (int i = tile.CanonicalTileId.Z - 1; i > 0; i--)
			{
				var cacheItem = MapboxAccess.Instance.CacheManager.GetTextureItemFromMemory(_sourceSettings.Id, parent.Canonical);
				if (cacheItem != null && cacheItem.Texture2D != null)
				{
					tile.SetParentTexture(parent, cacheItem.Texture2D);
					break;
				}

				parent = parent.Parent;
			}
		}
	}
}