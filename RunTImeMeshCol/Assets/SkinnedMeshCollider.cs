
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class SkinnedMeshCollider : MonoBehaviour
	{
		[SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;
		[SerializeField] private MeshCollider _meshCollider;

		private NativeArray<VertexData> _vertData;
		private ComputeBuffer _vertBuffer;
		private int _kernel;
		private int _dispatchCount;
		private AsyncGPUReadbackRequest _request;

		private ComputeShader _bakePointComputeShader;
		
		/// <summary>
		/// 頂点バッファ定義
		/// </summary>
		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		struct VertexData
		{
			public Vector3 pos;
			public Vector3 nor;
			public Vector2 uv;
		}

		private Mesh _mesh;
		private void Start()
		{
			Debug.Log(this.gameObject.name);

			_skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
			_meshCollider = GetComponent<MeshCollider>();

			if (_skinnedMeshRenderer == null)
			{
				return;
			}
			
			//頂点座標の入れ物であるMeshを一回Bakeする
			var mesh = _skinnedMeshRenderer.sharedMesh;
			_mesh = new Mesh();
			_mesh.name = "skinmesh";
			_skinnedMeshRenderer.BakeMesh(_mesh);
			
			//Computeshaderに
			_vertData = new NativeArray<VertexData>(mesh.vertexCount, Allocator.Temp);
			for (int i = 0; i < mesh.vertexCount; ++i)
			{
				VertexData v = new VertexData();
				v.pos = mesh.vertices[i];
				v.nor = mesh.normals[i];
				v.uv = mesh.uv[i];
				_vertData[i] = v;
			}
			var layout = new[] 
			{
				new VertexAttributeDescriptor(VertexAttribute.Position, _mesh.GetVertexAttributeFormat(VertexAttribute.Position), 3),
				new VertexAttributeDescriptor(VertexAttribute.Normal, _mesh.GetVertexAttributeFormat(VertexAttribute.Normal), 3),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord0, _mesh.GetVertexAttributeFormat(VertexAttribute.TexCoord0), 2),

			};
			_mesh.SetVertexBufferParams(mesh.vertexCount, layout);
			_vertBuffer = new ComputeBuffer(mesh.vertexCount, 8*4);

			if (_vertData.IsCreated) _vertBuffer.SetData(_vertData);
			
			//computeshaderをロード。
			_bakePointComputeShader = Instantiate(Resources.Load<ComputeShader>("SkinMeshtoMesh")) as ComputeShader;
			_kernel = _bakePointComputeShader.FindKernel("CSMain");
			uint threadX = 0;
			uint threadY = 0;
			uint threadZ = 0;
			_bakePointComputeShader.GetKernelThreadGroupSizes(_kernel, out threadX, out threadY, out threadZ);
			_dispatchCount = Mathf.CeilToInt(mesh.vertexCount / threadX + 1);

			_bakePointComputeShader.SetBuffer(_kernel, "vertexBuffer", _vertBuffer);
			
			//頂点バッファをRawとして定義。下記マニュアル参照
			//https://docs.unity3d.com/2021.2/Documentation/ScriptReference/SkinnedMeshRenderer-vertexBufferTarget.html
			_skinnedMeshRenderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

			
			_request = AsyncGPUReadback.Request(_vertBuffer);
		}

		private void Update()
		{
			if (_skinnedMeshRenderer == null)
			{
				return;
			}
			var buffer = _skinnedMeshRenderer.GetVertexBuffer();
			if (buffer == null)
			{
				//何故か起動直後の何フレームかはVertexBufferが取れない
				Debug.Log("No VertexBuffer");
				return;
			}
			_bakePointComputeShader.SetBuffer(_kernel, "Verts", buffer);
			_bakePointComputeShader.SetMatrix("LocalToWorld", _skinnedMeshRenderer.worldToLocalMatrix * _skinnedMeshRenderer.rootBone.localToWorldMatrix);
			_bakePointComputeShader.Dispatch(_kernel, _dispatchCount, 1,1);

			buffer.Release();

			if (_request.done && !_request.hasError)
			{
				//Readback and show result on texture
				_vertData = _request.GetData<VertexData>();

				//Update mesh
				_mesh.MarkDynamic();
				_mesh.SetVertexBufferData(_vertData, 0, 0, _vertData.Length);
				_mesh.RecalculateNormals();

				//Update to collider
				_meshCollider.sharedMesh = _mesh;

				//Request AsyncReadback again
				_request = AsyncGPUReadback.Request(_vertBuffer);
			}
			
		}

		void Release()
		{
			if(_vertBuffer!=null) _vertBuffer.Release();
			if (_bakePointComputeShader != null)
			{
				Destroy(_bakePointComputeShader);
			}

			_skinnedMeshRenderer = null;
			_meshCollider = null;
		}

		private void OnDestroy()
		{
			Release();
		}
	}
