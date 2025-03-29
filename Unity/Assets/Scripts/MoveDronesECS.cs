// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Example.Systems;
using TMPro;
using UnityEngine;

// ReSharper disable InconsistentNaming
public class MoveDronesECS : MonoBehaviour
{
    [SerializeField] private TMP_Text   count;
    [SerializeField] private TMP_Text   fpsText;
    
    public  Material        material;
    public  Mesh            mesh;
    
    private RenderParams    rp;
    private Matrix4x4[]     instData;
    private int             entityCount;
    private Shape           shape;
    private Drones          drones;
    
    void Start()
    {
        entityCount = 1024;
        drones = new Drones();
        drones.Initialize();
        drones.SetEntityCount(entityCount);
        drones.SetTargetPlane(500, 1.2f);
        UpdateGuiCount();
        rp          = new RenderParams(material);
        instData    = new Matrix4x4[drones.maxDroneCount];
        GameObject.Find("Editor Plane").gameObject.SetActive(false);
    }
    
    private void UpdateGuiCount() {
        count.text  = $"Count: {entityCount}";
    }
    
    private void SetShape (Shape shape)
    {
        this.shape = shape;
        switch (shape)
        {
            case Shape.Plane:	drones.SetTargetPlane(500, 1.2f); 		    break;
            case Shape.Cube:	drones.SetTargetCube (500, 1.2f);			break;
            case Shape.Ring:	drones.SetTargetRings(500, 24, 1.2f, 1);	break;
            case Shape.Rings:	drones.SetTargetRings(500, 20, 1.2f, 10);	break;
        }
    }
    
    public void SetTargetPlane()    => SetShape(Shape.Plane);
    public void SetTargetCube()     => SetShape(Shape.Cube);
    public void SetTargetRing()     => SetShape(Shape.Ring);
    public void SetTargetRings()    => SetShape(Shape.Rings);
    
    public void IncreaseCount() {
        entityCount = Math.Min(drones.maxDroneCount, entityCount * 2);
        drones.SetEntityCount(entityCount);
        SetShape(shape);
        UpdateGuiCount();
    }
    
    public void DecreaseCount() {
        entityCount = Math.Max(4, entityCount / 2);
        drones.SetEntityCount(entityCount);
        SetShape(shape);
        UpdateGuiCount();
    }

    private const int FPSSampleCount = 30;
    private readonly int[] fpsSamples = new int[FPSSampleCount];
    private int sampleIndex;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer instanceBuffer;

    private void UpdateFps()
    {
        var sum = 0;
        for (var i = 0; i < FPSSampleCount; i++)
        {
            sum += fpsSamples[i];
        }
        fpsText.text = $"FPS: {sum / FPSSampleCount}";
    }
    
    void Update()
    {
        fpsSamples[sampleIndex++] = (int)(1.0f / Time.deltaTime);
        if (sampleIndex >= FPSSampleCount) sampleIndex = 0;

        UpdateFps();

        var deltaTime = Time.deltaTime * 1000;

        UpdateDronesTransforms(deltaTime);

        UpdateTransformsArray();

        UpdateGraphicsMesh();
    }

    private void UpdateDronesTransforms(float deltaTime)
    {
        drones.UpdateTransforms(deltaTime, default);
    }

    private void UpdateTransformsArray()
    {
        int n = 0;
        var scale = Matrix4x4.Scale(new Vector3(10, 10, 10));
        foreach (var (transforms, _) in drones.transQuery.Chunks)
        {
            foreach (ref var trans in transforms.Span)
            {
                ref var data = ref instData[n++];
                data = trans.value.AsUnityMatrix4x4();
            }
        }
    }

    private void UpdateGraphicsMesh()
    {
        //Graphics.RenderMeshInstanced(rp, mesh, 0, instData, entityCount);
        InitializeDrawMesh();
    }

    private void InitializeDrawMesh()
    {
        var instanceCount = entityCount;

        //Matrix4x4[] matrices = new Matrix4x4[instanceCount];
        //for (int i = 0; i < instanceCount; i++)
        //{
        //    Vector3 position = Random.insideUnitSphere * 10f;
        //    matrices[i] = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
        //}

        instanceBuffer = new ComputeBuffer(instanceCount, 64); // 64 bytes per 4x4 matrix
        instanceBuffer.SetData(instData);

        // Step 2: Create the Indirect Draw Args Buffer
        uint[] args = new uint[5] { mesh.GetIndexCount(0), (uint)instanceCount, 0, 0, 0 };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        // Step 3: Bind instance buffer to material
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetBuffer("_InstanceData", instanceBuffer);

        // Step 4: Call DrawMeshInstancedIndirect
        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, new Bounds(Vector3.zero, Vector3.one * 1000), argsBuffer, 0, mpb);
    }
}
