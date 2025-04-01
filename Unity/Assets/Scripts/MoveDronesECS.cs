// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Example.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

// ReSharper disable InconsistentNaming
public class MoveDronesECS : MonoBehaviour
{
    [SerializeField] private TMP_Text count;
    [SerializeField] private TMP_Text fpsText;

    public Material material;
    public Mesh mesh;

    private RenderParams rp;
    private Matrix4x4[] instData;
    private int entityCount;
    private Shape shape;
    private Drones drones;

    private GraphicsBuffer instanceBuffer;

    void Start()
    {
        entityCount = 1024;
        drones = new Drones();
        drones.Initialize();
        drones.SetEntityCount(entityCount);
        drones.SetTargetPlane(500, 1.2f);
        UpdateGuiCount();
        rp = new RenderParams(material);
        instData = new Matrix4x4[drones.maxDroneCount];
        //GameObject.Find("Editor Plane").gameObject.SetActive(false);

        instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, instData.Length, sizeof(float) * 16);
    }

    private void OnDestroy()
    {
        instanceBuffer?.Dispose();
        instanceBuffer = null;
    }

    private void UpdateGuiCount()
    {
        count.text = $"Count: {entityCount}";
    }

    private void SetShape(Shape shape)
    {
        this.shape = shape;
        switch (shape)
        {
            case Shape.Plane: drones.SetTargetPlane(500, 1.2f); break;
            case Shape.Cube: drones.SetTargetCube(500, 1.2f); break;
            case Shape.Ring: drones.SetTargetRings(500, 24, 1.2f, 1); break;
            case Shape.Rings: drones.SetTargetRings(500, 20, 1.2f, 10); break;
        }
    }

    public void SetTargetPlane() => SetShape(Shape.Plane);
    public void SetTargetCube() => SetShape(Shape.Cube);
    public void SetTargetRing() => SetShape(Shape.Ring);
    public void SetTargetRings() => SetShape(Shape.Rings);

    public void IncreaseCount()
    {
        entityCount = Math.Min(drones.maxDroneCount, entityCount * 2);
        drones.SetEntityCount(entityCount);
        SetShape(shape);
        UpdateGuiCount();
    }

    public void DecreaseCount()
    {
        entityCount = Math.Max(4, entityCount / 2);
        drones.SetEntityCount(entityCount);
        SetShape(shape);
        UpdateGuiCount();
    }

    private const int FPSSampleCount = 30;
    private readonly int[] fpsSamples = new int[FPSSampleCount];
    private int sampleIndex;

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
        drones.UpdateTransforms(deltaTime, Matrix4x4.identity);
    }

    private void UpdateTransformsArray()
    {
        int n = 0;
        var scale = Matrix4x4.Scale(new Vector3(10, 10, 10));
        foreach (var (transforms, _) in drones.transQuery.Chunks)
        {
            foreach (ref var trans in transforms.Span)
            {
                //ref var data = ref instData[n++];
                //data = trans.value.AsUnityMatrix4x4();
                //instData[n++] = trans.value.AsUnityMatrix4x4();
                //SetValue(ref instData[n++], ref trans.value);
                instData[n++] = trans.value;
                //instData[n++] = trans.value.ToUnity();
            }
        }
    }

    private void SetValue(ref Matrix4x4 target, ref System.Numerics.Matrix4x4 source)
    {
        target.m00 = source.M11; target.m01 = source.M21; target.m02 = source.M31; target.m03 = source.M41;
        target.m10 = source.M12; target.m11 = source.M22; target.m12 = source.M32; target.m13 = source.M42;
        target.m20 = source.M13; target.m21 = source.M23; target.m22 = source.M33; target.m23 = source.M43;
        target.m30 = source.M14; target.m31 = source.M24; target.m32 = source.M34; target.m33 = source.M44;
    }

    private void UpdateGraphicsMesh()
    {
        Profiler.BeginSample("UpdateGraphicsMesh");

        instanceBuffer.SetData(instData);

        RenderParams rp = new RenderParams(material);
        rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one); // use tighter bounds
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetMatrix("_ObjectToWorld", Matrix4x4.Translate(new Vector3(0, 0, 0)));
        rp.matProps.SetFloat("_NumInstances", entityCount);
        rp.matProps.SetBuffer("_Transforms", instanceBuffer);

        Graphics.RenderMeshPrimitives(rp, mesh, 0, entityCount);

        //var n = 0;
        //foreach (var (transforms, _) in drones.transQuery.Chunks)
        //{
        //    foreach (ref var trans in transforms.Span)
        //    {
        //        instData[n++] = trans.value;
        //    }
        //}
        //Graphics.RenderMeshInstanced(rp, mesh, 0, instData, entityCount);

        Profiler.EndSample();
    }
}

