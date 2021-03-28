﻿using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Platform;
using UnityEngine;

namespace CustomImageLayerSample
{
	public class CustomImageTile : RasterTile
	{
		private string _urlFormat = "https://maps.aerisapi.com/anh3TB1Xu9Wr6cPndbPwF_EuOSGuqkH433UmnajaOP0MD9rpIh5dZ38g2SUwvu/flat,ftemperatures-max-text,admin/{0}/{1}/{2}/current.png";

		public CustomImageTile(CanonicalTileId tileId, string tilesetId, string format) : base(tileId, tilesetId)
		{
			_urlFormat = format;
		}

		internal override void Initialize(IFileSource fileSource, CanonicalTileId canonicalTileId, string tilesetId, Action p)
		{
			Cancel();

			TileState = TileState.Loading;
			Id = canonicalTileId;
			TilesetId = tilesetId;
			_callback = p;

			_unityRequest = fileSource.CustomImageRequest(GetURL(Id), HandleTileResponse);
		}

		private string GetURL(CanonicalTileId id)
		{
			return string.Format(_urlFormat, id.Z, id.X, id.Y);
		}


	}

	public class FileImageTile : RasterTile
	{
		public string FilePath;

		public FileImageTile(CanonicalTileId tileId, string tilesetId, string filePath) : base(tileId, tilesetId)
		{
			FilePath = filePath;
		}

		internal override void Initialize(IFileSource fileSource, CanonicalTileId canonicalTileId, string tilesetId, Action p)
		{
			Cancel();

			TileState = TileState.Loading;
			Id = canonicalTileId;
			TilesetId = tilesetId;
			_callback = p;

			_unityRequest = fileSource.CustomImageRequest(FilePath, HandleTileResponse);
		}
	}
}
