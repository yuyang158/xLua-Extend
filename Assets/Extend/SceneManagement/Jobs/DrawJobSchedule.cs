﻿using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Extend.SceneManagement.Jobs {
	public class BatchMeshMaterial : IComparable {
		public Mesh Mesh;
		public Material Material;
		public int SubMeshIndex;
		public ShadowCastingMode ShadowCastingMode;
		public bool ReceiveShadow;
		public int MeshMaterialIndex;
		public List<DrawInstance> Instances = new();
		private static readonly MaterialPropertyBlock m_emptyBlock = new();

		private DrawMatrixBuildSingleJob[] m_buildJob;
		private JobHandle[] m_jobHandle;
		private Material m_transparentMaterial;
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
		private static readonly int Surface = Shader.PropertyToID("_Surface");
		private static readonly int Blend = Shader.PropertyToID("_Blend");
		
		private RenderParams m_renderParams;

		public void Build() {
			var count = Mathf.CeilToInt(Instances.Count / 1024.0f);
			m_buildJob = new DrawMatrixBuildSingleJob[count];
			m_jobHandle = new JobHandle[count];
			Bounds worldBounds = new Bounds(Vector3.zero, Vector3.zero);
			for( int i = 0; i < count; i++ ) {
				m_buildJob[i] = new DrawMatrixBuildSingleJob();
				DrawInstance[] instances = i + 1 == count ? new DrawInstance[Instances.Count - i * 1024] : new DrawInstance[1024];
				Instances.CopyTo(i * 1024, instances, 0, instances.Length);
				m_buildJob[i].Init(instances);
				foreach( DrawInstance instance in instances ) {
					worldBounds.Encapsulate(instance.Bounds);
				}
			}
			Instances = null;
			m_renderParams = new RenderParams(Material) {
				receiveShadows = ReceiveShadow,
				shadowCastingMode = ShadowCastingMode,
				worldBounds = worldBounds,
				layer = 0
			};
		}

		public void SetVisible(int index, bool visible) {
			var jobIndex = Mathf.FloorToInt(index / 1024.0f);
			m_buildJob[jobIndex].SetVisible(index % 1024, visible);
		}

		public bool GetVisible(int index) {
			var jobIndex = Mathf.FloorToInt(index / 1024.0f);
			return m_buildJob[jobIndex].GetVisible(index % 1024);
		}

		public int CompareTo(object other) {
			var meshMaterial = (BatchMeshMaterial)other;
			return Material.renderQueue.CompareTo(meshMaterial.Material.renderQueue);
		}

		public void Dispose() {
			if( m_transparentMaterial ) {
				Object.Destroy(m_transparentMaterial);
			}
			foreach( var job in m_buildJob ) {
				job.Dispose();
			}
		}

		public void Schedule() {
			for( int i = 0; i < m_buildJob.Length; i++ ) {
				var job = m_buildJob[i];
				job.Reset();
				m_jobHandle[i] = job.StartSchedule();
			}
		}

		public void Draw() {
			foreach( var job in m_buildJob ) {
				if( job.GetVisibleCount() == 0 ) {
					continue;
				}
				
				var visibleCount = job.GetVisibleCount();
				var matrixArray = job.GetVisibleWorldArray();
				if( visibleCount == 1 ) {
					Graphics.DrawMesh(Mesh, matrixArray[0], Material, 0, Camera.main, SubMeshIndex, m_emptyBlock, ShadowCastingMode, ReceiveShadow);
				}
				else {
					Graphics.RenderMeshInstanced(m_renderParams, Mesh, SubMeshIndex, matrixArray, visibleCount);
				}
			}
		}

		public void DrawOneMesh(int index) {
			var jobIndex = Mathf.FloorToInt(index / 1024.0f);
			var worldMatrix = m_buildJob[jobIndex].GetWorldMatrix(index % 1024);
			Graphics.DrawMesh(Mesh, worldMatrix, Material, 0, Camera.main, SubMeshIndex, m_emptyBlock, ShadowCastingMode, ReceiveShadow);
		}

		public void DrawOneMeshInTransparent(int index) {
			var jobIndex = Mathf.FloorToInt(index / 1024.0f);
			var worldMatrix = m_buildJob[jobIndex].GetWorldMatrix(index % 1024);
			if( m_transparentMaterial == null ) {
				m_transparentMaterial = new Material(Material);
				m_transparentMaterial.SetFloat(Surface, 1);
				m_transparentMaterial.SetOverrideTag("RenderType", "Transparent");
				m_transparentMaterial.SetInt(SrcBlend, (int)BlendMode.SrcAlpha);
				m_transparentMaterial.SetInt(DstBlend, (int)BlendMode.OneMinusSrcAlpha);
				m_transparentMaterial.SetInt(ZWrite, 1);
				m_transparentMaterial.SetInt(Blend, 1);
				var baseColor = m_transparentMaterial.GetColor(BaseColor);
				baseColor.a = 0.1f;
				// m_transparentMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
				m_transparentMaterial.SetColor(BaseColor, baseColor);
				m_transparentMaterial.renderQueue = (int)RenderQueue.Transparent;
				m_transparentMaterial.enableInstancing = false;
			}
			Graphics.DrawMesh(Mesh, worldMatrix, m_transparentMaterial, 0, Camera.main, SubMeshIndex, m_emptyBlock, ShadowCastingMode, false);
		}

		public void Complete() {
			for( int i = 0; i < m_buildJob.Length; i++ ) {
				m_jobHandle[i].Complete();
			}
		}
	}

	public class DrawJobSchedule {
		private static int ComputeHash(Mesh mesh, Material material) {
			var meshID = mesh.GetInstanceID();
			var materialID = material.GetInstanceID();
			return $"{meshID}_{materialID}".GetHashCode();
		}

		private Dictionary<int, BatchMeshMaterial> m_meshMaterialContainer;
		private readonly List<BatchMeshMaterial> m_meshMaterials = new List<BatchMeshMaterial>();
		private int m_meshMaterialIndex;
		public List<DrawInstance> Instances = new List<DrawInstance>();

		public void Prepare() {
			m_meshMaterialContainer = new Dictionary<int, BatchMeshMaterial>(64);
			m_meshMaterialIndex = 0;
		}

		public void Schedule() {
			foreach( var meshMaterial in m_meshMaterials ) {
				meshMaterial.Schedule();
			}
		}

		public void Complete() {
			foreach( var meshMaterial in m_meshMaterials ) {
				meshMaterial.Complete();
			}
		}
		
		public void Draw() {
			foreach( var meshMaterial in m_meshMaterials ) {
				meshMaterial.Draw();
			}
		}

		public BatchMeshMaterial GetMeshMaterial(int index) {
			return m_meshMaterials[index];
		}

		public static int BuildCombinedID(int materialIndex, int instanceIndex) {
			return materialIndex * BatchMeshMaterialMap.MESH_MATERIAL_OFFSET + instanceIndex;
		}

		public static void SplitCombineID(int combineID, out int materialIndex, out int instanceIndex) {
			materialIndex = combineID / BatchMeshMaterialMap.MESH_MATERIAL_OFFSET;
			instanceIndex = combineID % BatchMeshMaterialMap.MESH_MATERIAL_OFFSET;
		}
		
		public void ConvertRenderer(Mesh mesh, MeshRenderer renderer) {
			if( mesh.subMeshCount != renderer.sharedMaterials.Length ) {
				Debug.LogError("Only supports one-to-one correspondence between SubMesh and material : " + renderer.name, renderer);
				return;
			}

			var batchMeshMaterialMap = renderer.gameObject.AddComponent<BatchMeshMaterialMap>();
			batchMeshMaterialMap.CombinedIDs = new int[mesh.subMeshCount];
			for( int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++ ) {
				var material = renderer.sharedMaterials[subMeshIndex];
				var hash = ComputeHash(mesh, material);
				if( !m_meshMaterialContainer.TryGetValue(hash, out var meshMaterial) ) {
					if( !material.enableInstancing ) {
						Debug.LogError($"{material.name} instancing is disabled", material);
						continue;
					}

					meshMaterial = new BatchMeshMaterial {
						Material = material,
						Mesh = mesh,
						SubMeshIndex = subMeshIndex,
						ShadowCastingMode = renderer.shadowCastingMode,
						ReceiveShadow = renderer.receiveShadows,
						MeshMaterialIndex = m_meshMaterialIndex
					};
					m_meshMaterialIndex++;
					m_meshMaterialContainer.Add(hash, meshMaterial);
					m_meshMaterials.Add(meshMaterial);
				}

				var instance = new DrawInstance {
					World = renderer.localToWorldMatrix,
					Bounds = renderer.bounds,
					MaterialIndex = meshMaterial.MeshMaterialIndex,
					Index = meshMaterial.Instances.Count,
					InstanceID = renderer.gameObject.GetInstanceID()
				};
				batchMeshMaterialMap.CombinedIDs[subMeshIndex] = BuildCombinedID(instance.MaterialIndex, instance.Index);
				meshMaterial.Instances.Add(instance);
				Instances.Add(instance);
			}
		}

		public void AfterBuild() {
			m_meshMaterialContainer = null;
			Instances.Clear();
			Instances = null;
			foreach( var meshMaterial in m_meshMaterials ) {
				meshMaterial.Build();
			}
		}

		public void Dispose() {
			foreach( var meshMaterial in m_meshMaterials ) {
				meshMaterial.Dispose();
			}
		}
	}
}
