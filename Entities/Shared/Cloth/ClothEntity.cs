﻿using Frosty.Core.Viewport;
using LevelEditorPlugin.Managers;
using LevelEditorPlugin.Render.Proxies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LevelEditorPlugin.Entities
{
    [EntityBinding(DataType = typeof(FrostySdk.Ebx.ClothEntityData))]
    public class ClothEntity : SpatialEntity, IEntityData<FrostySdk.Ebx.ClothEntityData>
    {
        public new FrostySdk.Ebx.ClothEntityData Data => data as FrostySdk.Ebx.ClothEntityData;
        public Assets.MeshAsset Mesh { get; private set; }

        public ClothEntity(FrostySdk.Ebx.ClothEntityData inData, Entity inParent)
            : base(inData, inParent)
        {
            Mesh = LoadedAssetManager.Instance.LoadAsset<Assets.MeshAsset>(this, Data.Mesh);
        }

        public override void CreateRenderProxy(List<RenderProxy> proxies, RenderCreateState state)
        {
            Mesh.LoadResource(state);
            proxies.Add(new ModelRenderProxy(state, this));

            SetFlags(EntityFlags.RenderProxyGenerated);
        }

        public override void Destroy()
        {
            LoadedAssetManager.Instance.UnloadAsset(Mesh);
        }
    }
}
