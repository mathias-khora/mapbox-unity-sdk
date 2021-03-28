﻿using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Platform.Cache;
using Mapbox.Unity;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using UnityEngine;
using UnityEditor;

public class MapboxDebugWindow : EditorWindow
{
	private int _currentTab;
	private string[] _tabList = new string[] {"DataFetcher", "UnityTiles" , "Memory Cache", "File Cache"};
	private static DataFetcherTabDebugView _dataFetcherTabDebugView;
	private static UnityTilesTabDebugView _unityTilesTabDebugView;
	private static MemoryTabDebugView _memoryTabDebugView;
	private static FileCacheDebugView _fileCacheDebugView;


	[MenuItem("Mapbox/Debug Window")]
	public static void ShowWindow()
	{
		ReadyObjects();
		EditorWindow.GetWindow(typeof(MapboxDebugWindow));
	}

	private static void ReadyObjects()
	{
		if (_dataFetcherTabDebugView == null)
		{
			_dataFetcherTabDebugView = new DataFetcherTabDebugView();
		}
		if (_unityTilesTabDebugView == null)
		{
			_unityTilesTabDebugView = new UnityTilesTabDebugView();
		}
		if (_memoryTabDebugView == null)
		{
			_memoryTabDebugView = new MemoryTabDebugView();
		}
		if (_fileCacheDebugView == null)
		{
			_fileCacheDebugView = new FileCacheDebugView();
		}
	}

	void OnGUI()
	{
		ReadyObjects();
		_currentTab = GUILayout.Toolbar (_currentTab, _tabList);
		switch (_currentTab)
		{
			case 0 : 
				_dataFetcherTabDebugView.Draw();
				break;
			case 1 :
				_unityTilesTabDebugView.Draw();
				break;
			case 2 :
				_memoryTabDebugView.Draw();
				break;
			case 3 :
				_fileCacheDebugView.Draw();
				break;
		}
	}

	public void OnInspectorUpdate()
	{
		// This will only get called 10 times per second.
		Repaint();
	}
}

public class MemoryTabDebugView
{
	private EditorMemoryCache _memoryCache;
	private bool _cachedFold;
	private bool _fixedFold;
	private bool _logFold;
	private bool[] _cachedItemFolds;
	private bool[] _fixedItemFolds;
	private Vector2 _cachedScrollPos;
	private Vector2 _fixedScrollPos;
	private int _currentTab;
	private string[] _tabList = new string[2] {"Group By Tile", "Group By Tileset"};

	private bool[] _cacheByTileFolds;
	private bool[] _cacheByTilesetFolds;

	private Queue<string> _logs;
	private Vector2 _logScrollPos;

	public MemoryTabDebugView()
	{
		_memoryCache = MapboxAccess.Instance.CacheManager.GetMemoryCache();
		_logs = new Queue<string>();
		_memoryCache.TileAdded += (s, id, arg3, arg4) =>
		{
			Log(string.Format("{0,10} - Tile Added    : {1}-{2}", Time.frameCount, id, s));
		};
		_memoryCache.TileReleased += (id, s) =>
		{
			Log(string.Format("{0,10} - Tile Disposed : {1}-{2}", Time.frameCount, id, s));
		};
		_memoryCache.TileRead += (id, s) =>
		{
			Log(string.Format("{0,10} - Tile Read     : {1}-{2}", Time.frameCount, id, s));
		};
		_memoryCache.TileSetFallback += (id, s) =>
		{
			Log(string.Format("{0,10} - Tile Fixated  : {1}-{2}", Time.frameCount, id, s));
		};
		_memoryCache.TilePruned += (id, s) =>
		{
			Log(string.Format("{0,10} - Tile Pruned   : {1}-{2}", Time.frameCount, id, s));
		};
	}

	private void Log(string s)
	{
		_logs.Enqueue(s);
		if (_logs.Count > 100)
		{
			_logs.Dequeue();
		}
		_logScrollPos = new Vector2(0, 40 * _logs.Count);
	}

	public void Draw()
	{
		var _cachedList = _memoryCache.GetCachedItems;
		var _fixedList = _memoryCache.GetFallbackItems;

		_cachedFold = EditorGUILayout.Foldout(_cachedFold, string.Format("Cached Items ({0})", _cachedList.Count));
		if (_cachedFold)
		{
			EditorGUI.indentLevel++;
			var index = 0;

			var _groupById = new Dictionary<CanonicalTileId, List<CacheItem>>();
			var _groupByTileset = new Dictionary<string, List<CacheItem>>();

			foreach (var item in _cachedList)
			{
				if (!_groupById.ContainsKey(item.Value.TileId))
				{
					_groupById.Add(item.Value.TileId, new List<CacheItem>() {item.Value});
				}
				else
				{
					_groupById[item.Value.TileId].Add(item.Value);
				}

				if (!_groupByTileset.ContainsKey(item.Value.TilesetId))
				{
					_groupByTileset.Add(item.Value.TilesetId, new List<CacheItem>() {item.Value});
				}
				else
				{
					_groupByTileset[item.Value.TilesetId].Add(item.Value);
				}
			}

			GUILayout.Label(string.Format("{0} tiles {1} tilesets", _groupById.Count, _groupByTileset), EditorStyles.miniLabel);

			_currentTab = GUILayout.Toolbar (_currentTab, _tabList);
			if (_currentTab == 0)
			{
				DrawCachesByTile(_groupById);
			}
			else if (_currentTab == 1)
			{
				DrawCachesByTileset(_groupByTileset);
			}


			EditorGUI.indentLevel--;
		}

		FixedFold(_fixedList);

		DrawLogs();
	}

	private void DrawLogs()
	{
		_logFold = EditorGUILayout.Foldout(_logFold, string.Format("Logs ({0})", _logs.Count));
		if (_logFold)
		{
			using (var h = new EditorGUILayout.HorizontalScope())
			{
				using (var scrollView = new EditorGUILayout.ScrollViewScope(_logScrollPos, GUILayout.Height(300)))
				{
					_logScrollPos = scrollView.scrollPosition;
					foreach (var log in _logs)
					{
						EditorGUILayout.LabelField(string.Format(log), EditorStyles.miniLabel);
					}
				}
			}
		}

	}

	private void FixedFold(Dictionary<int, CacheItem> _fixedList)
	{
		Array.Resize(ref _fixedItemFolds, _fixedList.Count);
		_fixedFold = EditorGUILayout.Foldout(_fixedFold, string.Format("Fixed Items ({0})", _fixedList.Count));
		if (_fixedFold)
		{
			EditorGUI.indentLevel++;
			var index = 0;

			EditorGUILayout.BeginHorizontal();
			_fixedScrollPos = EditorGUILayout.BeginScrollView(_fixedScrollPos, GUILayout.Height(500));

			foreach (var item in _fixedList)
			{
				_fixedItemFolds[index] = EditorGUILayout.Foldout(
					_fixedItemFolds[index],
					string.Format("{0} - {1}", item.Value.TileId, item.Value.TilesetId));
				if (_fixedItemFolds[index])
				{
					EditorGUI.indentLevel++;
					DrawCacheItem(item.Value);
					EditorGUI.indentLevel--;
				}

				index++;
			}

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel--;
		}
	}

	private void DrawCachesByTile(Dictionary<CanonicalTileId,List<CacheItem>> groupById)
	{
		Array.Resize(ref _cachedItemFolds, groupById.Count + 1);
		EditorGUILayout.BeginHorizontal();
		_cachedScrollPos = EditorGUILayout.BeginScrollView(_cachedScrollPos, GUILayout.Height(500), GUILayout.ExpandWidth(true));

		var index = 0;
		EditorGUILayout.BeginVertical();
		foreach (var tile in groupById)
		{
			_cachedItemFolds[index] = EditorGUILayout.Foldout(
				_cachedItemFolds[index],
				string.Format("{0} ({1})", tile.Key, tile.Value.Count));
			if (_cachedItemFolds[index])
			{
				EditorGUI.indentLevel++;
				Array.Resize(ref _cacheByTileFolds, tile.Value.Count + 1);
				var index2 = 0;
				foreach (var item in tile.Value)
				{
					_cacheByTileFolds[index2] = EditorGUILayout.Foldout(
						_cacheByTileFolds[index2],
						string.Format("{0}", item.TilesetId));
					if (_cacheByTileFolds[index2])
					{
						EditorGUI.indentLevel++;
						DrawCacheItem(item);
						EditorGUI.indentLevel--;
					}
					index2++;
				}
				//DrawCacheItem(item);
				EditorGUI.indentLevel--;
			}
			index++;
		}
		EditorGUILayout.EndVertical();
		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndHorizontal();
	}

	private void DrawCachesByTileset(Dictionary<string,List<CacheItem>> groupByTileset)
	{
		Array.Resize(ref _cachedItemFolds, groupByTileset.Count + 1);
		EditorGUILayout.BeginHorizontal();
		_cachedScrollPos = EditorGUILayout.BeginScrollView(_cachedScrollPos, GUILayout.Height(500), GUILayout.ExpandWidth(true));

		var index = 0;
		EditorGUILayout.BeginVertical();
		foreach (var tile in groupByTileset)
		{
			_cachedItemFolds[index] = EditorGUILayout.Foldout(
				_cachedItemFolds[index],
				string.Format("{0} ({1})", tile.Key, tile.Value.Count));
			if (_cachedItemFolds[index])
			{
				EditorGUI.indentLevel++;
				var index2 = 0;
				Array.Resize(ref _cacheByTileFolds, tile.Value.Count + 1);
				foreach (var item in tile.Value)
				{
					_cacheByTileFolds[index2] = EditorGUILayout.Foldout(
						_cacheByTileFolds[index2],
						string.Format("{0}", item.TileId));
					if (_cacheByTileFolds[index2])
					{
						EditorGUI.indentLevel++;
						DrawCacheItem(item);
						EditorGUI.indentLevel--;
					}
					index2++;
				}
				//DrawCacheItem(item);
				EditorGUI.indentLevel--;
			}
			index++;
		}
		EditorGUILayout.EndVertical();
		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndHorizontal();
	}

	private void DrawCacheItem(CacheItem item)
	{
		var cacheItem = (TextureCacheItem) item;
		if (cacheItem != null)
		{
			EditorGUILayout.LabelField(string.Format("Tile Id: {0}", cacheItem.TileId), EditorStyles.label);
			EditorGUILayout.LabelField(string.Format("Tileset {0}", cacheItem.TilesetId), EditorStyles.label);
			EditorGUILayout.LabelField(string.Format("From {0}", cacheItem.From), EditorStyles.label);
			EditorGUILayout.LabelField(string.Format("Expiration {0}", cacheItem.ExpirationDate), EditorStyles.label);
			if (cacheItem.Texture2D != null)
			{
				EditorGUILayout.ObjectField(cacheItem.Texture2D, typeof(Texture2D));
			}
		}
	}
}

public class UnityTilesTabDebugView
{
	private AbstractMap _map;
	public UnityTilesTabDebugView()
	{
		_map = GameObject.FindObjectOfType<AbstractMap>();
	}

	private Tile _openDataTile;
	private bool _activeTilesFold;
	private bool _inactiveTilesFold;
	private bool[] _subFoldList1;
	private bool[] _subFoldList2;

	public void Draw()
	{
		GUILayout.Label ("Unity Tiles", EditorStyles.boldLabel);
		var tiles = _map.MapVisualizer.ActiveTiles;
		var inactiveTiles = _map.MapVisualizer.GetInactiveTiles;

		GUILayout.Label (string.Format("{0} : {1}", "Unity Tile Count", tiles.Count), EditorStyles.miniLabel);

		DrawActiveTiles(tiles);

		DrawInactiveTiles(inactiveTiles);
	}

	private void DrawInactiveTiles(Queue<UnityTile> tiles)
	{
		_inactiveTilesFold = EditorGUILayout.Foldout(_inactiveTilesFold, string.Format("Inactive Tiles ({0})", tiles.Count));
		if (_inactiveTilesFold)
		{
			Array.Resize(ref _subFoldList1, tiles.Count);
			var zoomLevelDictionary = new Dictionary<int, int>();
			var dataTileDictionary = new Dictionary<Type, Tuple<int, int>>();
			var index = 0;
			foreach (var unityTile in tiles)
			{
				var zoom = unityTile.CurrentZoom;
				if (!zoomLevelDictionary.ContainsKey(zoom))
				{
					zoomLevelDictionary.Add(zoom, 1);
				}
				else
				{
					zoomLevelDictionary[zoom]++;
				}

				var dataErrors = new List<string>();
				foreach (var tile in unityTile.Tiles)
				{
					if (tile.HasError)
					{
						foreach (var exception in tile.Exceptions)
						{
							dataErrors.Add(exception.ToString());
						}
					}

					var type = tile.GetType();
					var isFromCacheAdd = tile.FromCache != CacheType.NoCache ? 1 : 0;
					if (!dataTileDictionary.ContainsKey(type))
					{
						dataTileDictionary.Add(type, new Tuple<int, int>(1, isFromCacheAdd));
					}
					else
					{
						dataTileDictionary[type] = new Tuple<int, int>(dataTileDictionary[type].Item1 + 1, dataTileDictionary[type].Item2 + isFromCacheAdd);
					}
				}

				_subFoldList1[index] = EditorGUILayout.Foldout(_subFoldList1[index], unityTile.CanonicalTileId.ToString());
				if (_subFoldList1[index])
				{
					EditorGUI.indentLevel++;
					foreach (var dataTile in unityTile.Tiles)
					{
						var dataTileFold = EditorGUILayout.Foldout(_openDataTile == dataTile, dataTile.GetType().ToString());
						if (dataTileFold)
						{
							EditorGUI.indentLevel++;
							_openDataTile = dataTile;
							if (dataTile is RasterTile)
							{
								if ((dataTile as RasterTile).Texture2D != null)
								{
									EditorGUILayout.ObjectField(
										(dataTile as RasterTile).Texture2D,
										typeof(Texture2D));
								}
							}

							EditorGUILayout.LabelField(string.Format("Tileset : {0}", dataTile.TilesetId), EditorStyles.miniLabel);
							EditorGUILayout.LabelField(string.Format("State : {0}", dataTile.CurrentTileState), EditorStyles.miniLabel);
							EditorGUILayout.LabelField(string.Format("Is Mapbox : {0}", dataTile.IsMapboxTile), EditorStyles.miniLabel);
							EditorGUILayout.LabelField(string.Format("From : {0}", dataTile.FromCache), EditorStyles.miniLabel);
							if (dataTile.HasError)
							{
								EditorGUILayout.LabelField(string.Format("Error : {0}", dataTile.Exceptions[0].Message), EditorStyles.miniLabel);
							}

							EditorGUI.indentLevel--;
						}
					}

					EditorGUI.indentLevel--;
				}

				index++;
			}

			foreach (var entry in zoomLevelDictionary)
			{
				GUILayout.Label(string.Format("{1} tiles at zoom level {0}", entry.Key, entry.Value), EditorStyles.miniLabel);
			}

			foreach (var entry in dataTileDictionary)
			{
				GUILayout.Label(string.Format("{1} data tile of type {0}", entry.Key, entry.Value), EditorStyles.miniLabel);
			}
		}
	}

	private void DrawActiveTiles(Dictionary<UnwrappedTileId, UnityTile> tiles)
	{
		_activeTilesFold = EditorGUILayout.Foldout(_activeTilesFold, string.Format("Active Tiles ({0})", tiles.Count));
		if (_activeTilesFold)
		{
			Array.Resize(ref _subFoldList2, tiles.Count);
			var zoomLevelDictionary = new Dictionary<int, int>();
			var dataTileDictionary = new Dictionary<Type, Tuple<int, int>>();
			var index = 0;
			foreach (var unityTile in tiles)
			{
				var zoom = unityTile.Value.CurrentZoom;
				if (!zoomLevelDictionary.ContainsKey(zoom))
				{
					zoomLevelDictionary.Add(zoom, 1);
				}
				else
				{
					zoomLevelDictionary[zoom]++;
				}

				var dataErrors = new List<string>();
				foreach (var tile in unityTile.Value.Tiles)
				{
					if (tile.HasError)
					{
						foreach (var exception in tile.Exceptions)
						{
							dataErrors.Add(exception.ToString());
						}
					}

					var type = tile.GetType();
					var isFromCacheAdd = tile.FromCache != CacheType.NoCache ? 1 : 0;
					if (!dataTileDictionary.ContainsKey(type))
					{
						dataTileDictionary.Add(type, new Tuple<int, int>(1, isFromCacheAdd));
					}
					else
					{
						dataTileDictionary[type] = new Tuple<int, int>(dataTileDictionary[type].Item1 + 1, dataTileDictionary[type].Item2 + isFromCacheAdd);
					}
				}

				_subFoldList2[index] = EditorGUILayout.Foldout(_subFoldList2[index], unityTile.Value.CanonicalTileId.ToString());
				if (_subFoldList2[index])
				{
					EditorGUI.indentLevel++;
					foreach (var dataTile in unityTile.Value.Tiles)
					{
						var dataTileFold = EditorGUILayout.Foldout(_openDataTile == dataTile, dataTile.GetType().ToString());
						if (dataTileFold)
						{
							EditorGUI.indentLevel++;
							_openDataTile = dataTile;
							if (dataTile is RasterTile)
							{
								if ((dataTile as RasterTile).Texture2D != null)
								{
									EditorGUILayout.ObjectField(
										(dataTile as RasterTile).Texture2D,
										typeof(Texture2D));
								}
							}

							EditorGUILayout.LabelField(string.Format("Tileset : {0}", dataTile.TilesetId), EditorStyles.miniLabel);
							EditorGUILayout.LabelField(string.Format("State : {0}", dataTile.CurrentTileState), EditorStyles.miniLabel);
							EditorGUILayout.LabelField(string.Format("Is Mapbox : {0}", dataTile.IsMapboxTile), EditorStyles.miniLabel);
							EditorGUILayout.LabelField(string.Format("From : {0}", dataTile.FromCache), EditorStyles.miniLabel);
							if (dataTile.HasError)
							{
								EditorGUILayout.LabelField(string.Format("Error : {0}", dataTile.Exceptions[0].Message), EditorStyles.miniLabel);
							}

							EditorGUI.indentLevel--;
						}
					}

					EditorGUI.indentLevel--;
				}

				index++;
			}

			foreach (var entry in zoomLevelDictionary)
			{
				GUILayout.Label(string.Format("{1} tiles at zoom level {0}", entry.Key, entry.Value), EditorStyles.miniLabel);
			}

			foreach (var entry in dataTileDictionary)
			{
				GUILayout.Label(string.Format("{1} data tile of type {0}", entry.Key, entry.Value), EditorStyles.miniLabel);
			}

		}
	}
}

public class DataFetcherTabDebugView
{
	private DebuggerDataFetcherWrapper _dataFetcher;

	public DataFetcherTabDebugView()
	{
		_dataFetcher = new DebuggerDataFetcherWrapper();
	}

	public void Draw()
	{
		var order = _dataFetcher.GetTileOrderQueue();
		var items = _dataFetcher.GetFetchInfoQueue();
		var activeRequests = _dataFetcher.GetActiveRequests();
		var activeRequestsLimit = _dataFetcher.GetActiveRequestLimit();

		GUILayout.Label ("Data Fetcher", EditorStyles.boldLabel);
		GUILayout.Label (string.Format("{0} : {1}/{2}", "Active Request Count", activeRequests.Count.ToString(), activeRequestsLimit), EditorStyles.miniLabel);
		GUILayout.Label (string.Format("{0} : {1}", "Order Queue Size", order.Count.ToString()), EditorStyles.miniLabel);
		GUILayout.Label (string.Format("{0} : {1}", "Item List Size", items.Count.ToString()), EditorStyles.miniLabel);
		GUILayout.Label (string.Format("{0} : {1}", "Order - Item (Cancelled)", order.Count - items.Count), EditorStyles.miniLabel);

		GUILayout.Space(10);
		var mapboxTiles = 0;
		var customTiles = 0;
		var tilesetDictionary = new Dictionary<string, int>();
		foreach (var item in items)
		{
			if (item.Value.RasterTile.IsMapboxTile)
			{
				mapboxTiles++;
			}
			else
			{
				customTiles++;
			}

			if (!tilesetDictionary.ContainsKey(item.Value.TilesetId))
			{
				tilesetDictionary.Add(item.Value.TilesetId, 1);
			}
			else
			{
				tilesetDictionary[item.Value.TilesetId]++;
			}
		}

		GUILayout.Label (string.Format("{0} : {1}", "Mapbox tiles", mapboxTiles.ToString()), EditorStyles.miniLabel);
		GUILayout.Label (string.Format("{0} : {1}", "Custom tiles", customTiles.ToString()), EditorStyles.miniLabel);

		GUILayout.Space(10);
		GUILayout.Label (string.Format("{0}", "Tilesets in list"), EditorStyles.miniLabel);
		foreach (var entry in tilesetDictionary)
		{
			GUILayout.Label (string.Format("{0} : {1}", entry.Key, entry.Value), EditorStyles.miniLabel);
		}
	}
}

public class DebuggerDataFetcherWrapper : DataFetcher
{
	public Queue<int> GetTileOrderQueue()
	{
		return _tileOrder;
	}

	public Dictionary<int, FetchInfo> GetFetchInfoQueue()
	{
		return _tileFetchInfos;
	}

	public int GetActiveRequestLimit()
	{
		return _activeRequestLimit;
	}

	public Dictionary<int, Tile> GetActiveRequests()
	{
		return _globalActiveRequests;
	}
}

public class FileCacheDebugView
{
	private static Queue<string> SavedLogs;

	private EditorFileCache _fileCache;
	private Vector2 _logsScrollPos;

	public FileCacheDebugView()
	{
		_fileCache = MapboxAccess.Instance.CacheManager.GetFileCache();
		SavedLogs = new Queue<string>();
	}

	public static void AddToLogs(string s)
	{
		SavedLogs.Enqueue(s);
		if (SavedLogs.Count > 50)
		{
			SavedLogs.Dequeue();
		}
	}

	public void Draw()
	{
		var infosToSaveCount = _fileCache.GetInfosToSaveListCount();

		GUILayout.Label(string.Format("Info in queue for saving : {0}", infosToSaveCount), EditorStyles.label);

		EditorGUILayout.BeginHorizontal();
		_logsScrollPos = EditorGUILayout.BeginScrollView(_logsScrollPos, GUILayout.Height(300), GUILayout.ExpandWidth(true));

		foreach (var log in SavedLogs)
		{
			GUILayout.Label(log, EditorStyles.miniLabel);
		}
		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndHorizontal();
	}
}