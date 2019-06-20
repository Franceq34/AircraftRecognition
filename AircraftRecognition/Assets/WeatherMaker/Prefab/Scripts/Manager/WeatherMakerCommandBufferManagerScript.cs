//
// Weather Maker for Unity
// (c) 2016 Digital Ruby, LLC
// Source code may be used for personal or commercial projects.
// Source code may NOT be redistributed or sold.
// 
// *** A NOTE ABOUT PIRACY ***
// 
// If you got this asset from a pirate site, please consider buying it from the Unity asset store at https://www.assetstore.unity3d.com/en/#!/content/60955?aid=1011lGnL. This asset is only legally available from the Unity Asset Store.
// 
// I'm a single indie dev supporting my family by spending hundreds and thousands of hours on this and other assets. It's very offensive, rude and just plain evil to steal when I (and many others) put so much hard work into the software.
// 
// Thank you.
//
// *** END NOTE ABOUT PIRACY ***
//

// #define ENABLE_FORWARD_OPAQUE_CAPTURE

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

#if UNITY_LWRP

using UnityEngine.Rendering.LWRP;

#endif

namespace DigitalRuby.WeatherMaker
{
    /// <summary>
    /// Weather maker camera types
    /// </summary>
    public enum WeatherMakerCameraType
    {
        /// <summary>
        /// Normal
        /// </summary>
        Normal,

        /// <summary>
        /// Reflection (water, mirror, etc.)
        /// </summary>
        Reflection,

        /// <summary>
        /// Cube map (reflection probe, etc.)
        /// </summary>
        CubeMap,

        /// <summary>
        /// Pre-render or other camera, internal use, should generally be ignored
        /// </summary>
        Other
    }

    /// <summary>
    /// Represents a command buffer
    /// </summary>
    public class WeatherMakerCommandBuffer
    {
        /// <summary>
        /// Camera the command buffer is attached to
        /// </summary>
        public Camera Camera;

        /// <summary>
        /// Render queue for the command buffer
        /// </summary>
        public CameraEvent RenderQueue;

        /// <summary>
        /// The command buffer
        /// </summary>
        public CommandBuffer CommandBuffer;

        /// <summary>
        /// A copy of the original material to render with, will be destroyed when command buffer is removed
        /// </summary>
        public Material Material;

        /// <summary>
        /// Reprojection state or null if none
        /// </summary>
        public WeatherMakerTemporalReprojectionState ReprojectionState;

        /// <summary>
        /// Whether the command buffer is a reflection
        /// </summary>
        public WeatherMakerCameraType CameraType { get; set; }
    }

    /// <summary>
    /// Command buffer manager
    /// </summary>
    [ExecuteInEditMode]
    public class WeatherMakerCommandBufferManagerScript : MonoBehaviour
    {
        [Tooltip("Material to downsample the depth buffer")]
        public Material DownsampleDepthMaterial;

        /// <summary>
        /// Set this in OnWillRenderObject for the current reflection Vector
        /// </summary>
        public static Vector3? CurrentReflectionPlane;

        /// <summary>
        /// Set to any camera you are calling RenderToCubemap with, null out after the call to RenderToCubemap
        /// </summary>
        public static Camera CubemapCamera;

        private readonly List<WeatherMakerCommandBuffer> commandBuffers = new List<WeatherMakerCommandBuffer>();
        private readonly List<KeyValuePair<System.Action<Camera>, MonoBehaviour>> preCullEvents = new List<KeyValuePair<System.Action<Camera>, MonoBehaviour>>();
        private readonly List<KeyValuePair<System.Action<Camera>, MonoBehaviour>> preRenderEvents = new List<KeyValuePair<System.Action<Camera>, MonoBehaviour>>();
        private readonly List<KeyValuePair<System.Action<Camera>, MonoBehaviour>> postRenderEvents = new List<KeyValuePair<System.Action<Camera>, MonoBehaviour>>();
        private readonly List<Camera> cameraStack = new List<Camera>();

        private const string depthCommandBufferName = "WeatherMakerDepthDownsample";
        private CommandBuffer depthCommandBuffer;

#if ENABLE_FORWARD_OPAQUE_CAPTURE

        private const string afterForwardOpaqueCommandBufferName = "WeatherMakerAfterForwardOpaque";
        private CommandBuffer afterForwardOpaqueCommandBuffer;
        public RenderTexture AfterOpaqueBuffer { get; private set; }

#endif

#if COPY_FULL_DEPTH_TEXTURE

        private RenderTexture depthBuffer;

#endif

        public RenderTexture HalfDepthBuffer { get; private set; }
        public RenderTexture QuarterDepthBuffer { get; private set; }
        public RenderTexture EighthDepthBuffer { get; private set; }
        public RenderTexture SixteenthDepthBuffer { get; private set; }

#if COPY_FULL_DEPTH_TEXTURE

        public RenderTargetIdentifier DepthBufferId { get; private set; }

#endif

        /// <summary>
        /// Current camera stack count
        /// </summary>
        public static int CameraStack { get { return (Instance == null ? 0 : Instance.cameraStack.Count); } }
        public static Camera BaseCamera { get { return (Instance == null || Instance.cameraStack.Count == 0 ? null : Instance.cameraStack[0]); } }

        private readonly Matrix4x4[] view = new Matrix4x4[2];
        private readonly Matrix4x4[] inverseView = new Matrix4x4[2];
        private readonly Matrix4x4[] inverseProj = new Matrix4x4[2];

        private void UpdateDeferredShadingKeyword(Camera camera)
        {
            if (camera.actualRenderingPath == RenderingPath.DeferredShading)
            {
                Shader.EnableKeyword("WEATHER_MAKER_DEFERRED_SHADING");
            }
            else
            {
                Shader.DisableKeyword("WEATHER_MAKER_DEFERRED_SHADING");
            }
        }

        private void SetupCommandBufferForCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            WeatherMakerCameraType cameraType = WeatherMakerScript.GetCameraType(camera);
            Camera baseCamera = WeatherMakerCommandBufferManagerScript.BaseCamera;
            UpdateDeferredShadingKeyword(camera);
            if (camera.stereoEnabled)
            {
                view[0] = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
                view[1] = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right);

                // see https://github.com/chriscummings100/worldspaceposteffect/blob/master/Assets/WorldSpacePostEffect/WorldSpacePostEffect.cs
                inverseView[0] = view[0].inverse;
                inverseView[1] = view[1].inverse;

                // only use base camera projection
                inverseProj[0] = baseCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left).inverse;
                inverseProj[1] = baseCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right).inverse;
            }
            else
            {
                view[0] = view[1] = camera.worldToCameraMatrix;

                inverseView[0] = inverseView[1] = view[0].inverse;

                // only use base camera projection
                inverseProj[0] = inverseProj[1] = baseCamera.projectionMatrix.inverse;
            }
            Shader.SetGlobalMatrixArray(WMS._WeatherMakerInverseView, inverseView);
            Shader.SetGlobalMatrixArray(WMS._WeatherMakerInverseProj, inverseProj);
            Shader.SetGlobalMatrixArray(WMS._WeatherMakerView, view);
            if (cameraType == WeatherMakerCameraType.CubeMap || camera == WeatherMakerCommandBufferManagerScript.CubemapCamera)
            {
                Shader.SetGlobalFloat(WMS._WeatherMakerCameraRenderMode, 2.0f);
            }
            else if (cameraType == WeatherMakerCameraType.Reflection)
            {
                Shader.SetGlobalFloat(WMS._WeatherMakerCameraRenderMode, 1.0f);
            }
            else
            {
                Shader.SetGlobalFloat(WMS._WeatherMakerCameraRenderMode, 0.0f);
            }
        }

        private void CleanupCommandBuffer(WeatherMakerCommandBuffer commandBuffer)
        {
            if (commandBuffer == null)
            {
                return;
            }
            else if (commandBuffer.Material != null && commandBuffer.Material.name.IndexOf("(clone)", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                GameObject.DestroyImmediate(commandBuffer.Material);
            }
            if (commandBuffer.Camera != null)
            {
                commandBuffer.Camera.RemoveCommandBuffer(commandBuffer.RenderQueue, commandBuffer.CommandBuffer);
            }
            if (commandBuffer.CommandBuffer != null)
            {
                commandBuffer.CommandBuffer.Dispose();
            }
        }

        private void CleanupCameras()
        {
            // remove destroyed camera command buffers
            for (int i = commandBuffers.Count - 1; i >= 0; i--)
            {
                if (commandBuffers[i].Camera == null)
                {
                    CleanupCommandBuffer(commandBuffers[i]);
                    commandBuffers.RemoveAt(i);
                }
            }
        }

        private void RemoveAllCommandBuffers()
        {
            for (int i = commandBuffers.Count - 1; i >= 0; i--)
            {
                CleanupCommandBuffer(commandBuffers[i]);
            }
            commandBuffers.Clear();
        }

        private void Update()
        {
            UpdateDepthDownsampler();
        }

        private void LateUpdate()
        {
            CleanupCameras();
        }

        private void OnEnable()
        {
            // use pre-render to give all other pre-cull scripts a chance to set properties, state, etc.

#if UNITY_LWRP

            RenderPipelineManager.beginCameraRendering += CameraBeginRendering;
            RenderPipelineManager.endCameraRendering += CameraEndRendering;
            RenderPipelineManager.beginFrameRendering += CameraBeginFrameRendering;
            RenderPipelineManager.endFrameRendering += CameraEndFrameRendering;

#else

            Camera.onPreCull += CameraPreCull;
            Camera.onPreRender += CameraPreRender;
            Camera.onPostRender += CameraPostRender;

#endif

            CleanupDepthTextures();
        }

        private void OnDisable()
        {
            // use pre-render to give all other pre-cull scripts a chance to set properties, state, etc.

#if UNITY_LWRP

            RenderPipelineManager.beginCameraRendering -= CameraBeginRendering;
            RenderPipelineManager.endCameraRendering -= CameraEndRendering;
            RenderPipelineManager.beginFrameRendering -= CameraBeginFrameRendering;
            RenderPipelineManager.endFrameRendering -= CameraEndFrameRendering;

#else

            Camera.onPreCull -= CameraPreCull;
            Camera.onPreRender -= CameraPreRender;
            Camera.onPostRender -= CameraPostRender;

#endif

            CleanupDepthTextures();

#if ENABLE_FORWARD_OPAQUE_CAPTURE

            if (AfterOpaqueBuffer != null)
            {
                AfterOpaqueBuffer.Release();
                Destroy(AfterOpaqueBuffer);
                AfterOpaqueBuffer = null;
            }
            AfterOpaqueBuffer = WeatherMakerFullScreenEffect.DestroyRenderTexture(AfterOpaqueBuffer);

#endif

        }

#if UNITY_LWRP

        private ScriptableRenderContext currentLWRPContext;

        private void CameraBeginRendering(ScriptableRenderContext context, Camera camera)
        {
            CameraPreCull(camera);
            CameraPreRender(camera);
        }

        private void CameraEndRendering(ScriptableRenderContext context, Camera camera)
        {
            CameraPostRender(camera);
        }

        private void CameraBeginFrameRendering(ScriptableRenderContext ctx, Camera[] cameras)
        {
            currentLWRPContext = ctx;
        }

        private void CameraEndFrameRendering(ScriptableRenderContext ctx, Camera[] cameras)
        {

        }

#endif

        private bool ListHasScript(List<KeyValuePair<System.Action<Camera>, MonoBehaviour>> list, MonoBehaviour script)
        {
            foreach (KeyValuePair<System.Action<Camera>, MonoBehaviour> item in list)
            {
                if (item.Value == script)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Register for pre cull events. Call from OnEnable.
        /// </summary>
        /// <param name="action">Action</param>
        /// <param name="script">Script</param>
        public void RegisterPreCull(System.Action<Camera> action, MonoBehaviour script)
        {
            if (script != null && !ListHasScript(preCullEvents, script))
            {
                preCullEvents.Add(new KeyValuePair<System.Action<Camera>, MonoBehaviour>(action, script));
            }
        }

        /// <summary>
        /// Unregister pre cull events. Call from OnDestroy.
        /// </summary>
        /// <param name="script">Script</param>
        public void UnregisterPreCull(MonoBehaviour script)
        {
            if (script != null)
            {
                for (int i = preCullEvents.Count - 1; i >= 0; i--)
                {
                    if (preCullEvents[i].Value == script)
                    {
                        preCullEvents.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Register pre render events. Call from OnEnable.
        /// </summary>
        /// <param name="action">Action</param>
        /// <param name="script">Script</param>
        /// <param name="highPriority">High priority go to front of the list, low to the back</param>
        public void RegisterPreRender(System.Action<Camera> action, MonoBehaviour script, bool highPriority = false)
        {
            if (script != null && !ListHasScript(preRenderEvents, script))
            {
                if (highPriority)
                {
                    preRenderEvents.Add(new KeyValuePair<System.Action<Camera>, MonoBehaviour>(action, script));
                }
                else
                {
                    preRenderEvents.Insert(0, new KeyValuePair<System.Action<Camera>, MonoBehaviour>(action, script));
                }
            }
        }

        /// <summary>
        /// Unregister pre render events. Call from OnDestroy.
        /// </summary>
        /// <param name="script">Script</param>
        public void UnregisterPreRender(MonoBehaviour script)
        {
            if (script != null)
            {
                for (int i = preRenderEvents.Count - 1; i >= 0; i--)
                {
                    if (preRenderEvents[i].Value == script)
                    {
                        preRenderEvents.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Register post render events. Call from OnEnable.
        /// </summary>
        /// <param name="action">Action</param>
        /// <param name="script">Script</param>
        public void RegisterPostRender(System.Action<Camera> action, MonoBehaviour script)
        {
            if (script != null && !ListHasScript(postRenderEvents, script))
            {
                postRenderEvents.Add(new KeyValuePair<System.Action<Camera>, MonoBehaviour>(action, script));
            }
        }

        /// <summary>
        /// Unregister post render events. Call from OnDestroy.
        /// </summary>
        /// <param name="script">Script</param>
        public void UnregisterPostRender(MonoBehaviour script)
        {
            if (script != null)
            {
                for (int i = postRenderEvents.Count - 1; i >= 0; i--)
                {
                    if (postRenderEvents[i].Value == script)
                    {
                        postRenderEvents.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Render a camera, handles LWRP, etc.
        /// </summary>
        /// <param name="camera">Camera to render</param>
        public void RenderCamera(Camera camera)
        {

#if UNITY_LWRP

            // for LWRP, these events are not invoked in RenderSingleCamera
            CameraPreCull(camera);
            CameraPreRender(camera);
            UnityEngine.Rendering.LWRP.LightweightRenderPipeline.RenderSingleCamera(currentLWRPContext, camera);
            CameraPostRender(camera);

#else

            camera.Render();

#endif

        }

        /// <summary>
        /// Add a command buffer to keep track of during rendering
        /// </summary>
        /// <param name="cmdBuffer">Command buffer</param>
        public void AddCommandBuffer(WeatherMakerCommandBuffer cmdBuffer)
        {
            if (!commandBuffers.Contains(cmdBuffer))
            {
                commandBuffers.Add(cmdBuffer);
            }
        }

        /// <summary>
        /// Remove a command buffer from rendering
        /// </summary>
        /// <param name="cmdBuffer">Command buffer</param>
        public void RemoveCommandBuffer(WeatherMakerCommandBuffer cmdBuffer)
        {
            commandBuffers.Remove(cmdBuffer);
        }

        private void CreateAfterForwardOpaqueCommandBuffer(Camera camera)
        {

#if ENABLE_FORWARD_OPAQUE_CAPTURE

            if (afterForwardOpaqueCommandBuffer != null)
            {
                camera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, afterForwardOpaqueCommandBuffer);
                camera.RemoveCommandBuffer(CameraEvent.AfterSkybox, afterForwardOpaqueCommandBuffer);
            }
            if (afterForwardOpaqueCommandBuffer == null || (AfterOpaqueBuffer != null && (AfterOpaqueBuffer.width != camera.pixelWidth || AfterOpaqueBuffer.height != camera.pixelHeight)))
            {
                if (AfterOpaqueBuffer != null)
                {
                    AfterOpaqueBuffer.Release();
                    Destroy(AfterOpaqueBuffer);
                    AfterOpaqueBuffer = WeatherMakerFullScreenEffect.CreateRenderTexture(WeatherMakerFullScreenEffect.GetRenderTextureDescriptor(1, 1, 1, RenderTextureFormat.DefaultHDR, 0, camera));
                }
                afterForwardOpaqueCommandBuffer = new CommandBuffer { name = afterForwardOpaqueCommandBufferName + Time.unscaledDeltaTime };
                afterForwardOpaqueCommandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, AfterOpaqueBuffer);
                afterForwardOpaqueCommandBuffer.SetGlobalTexture(WMS._CameraOpaqueTexture, AfterOpaqueBuffer);
            }
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                camera.AddCommandBuffer(CameraEvent.AfterSkybox, afterForwardOpaqueCommandBuffer);
            }
            else
            {
                camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, afterForwardOpaqueCommandBuffer);
            }

#endif

        }

        private void CreateAndAddDepthCommandBuffer(Camera camera)
        {
            if (HalfDepthBuffer == null)
            {
                return;
            }
            else if (camera.depthTextureMode == DepthTextureMode.None)
            {
                camera.depthTextureMode = DepthTextureMode.Depth;
            }

            bool deferred =

#if UNITY_LWRP

            // TODO: Revisit with LWRP deferred
            false;

#else

            (camera.actualRenderingPath == RenderingPath.DeferredLighting || camera.actualRenderingPath == RenderingPath.DeferredShading);

#endif

            depthCommandBuffer = (depthCommandBuffer == null ? new CommandBuffer { name = "WeatherMakerDownsampleDepth" } : depthCommandBuffer);
            camera.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, depthCommandBuffer);
            camera.RemoveCommandBuffer(CameraEvent.BeforeReflections, depthCommandBuffer);
            depthCommandBuffer.Clear();
            if (deferred && UnityEngine.XR.XRDevice.isPresent)
            {
                // bug in VR, deferred depth texture not set
                depthCommandBuffer.SetGlobalTexture(WMS._CameraDepthTexture, BuiltinRenderTextureType.ResolvedDepth);
            }

#if COPY_FULL_DEPTH_TEXTURE

            depthCommandBuffer.SetGlobalFloat(WMS._DownsampleDepthScale, 1.0f);
            depthCommandBuffer.Blit(HalfDepthBufferId, DepthBufferId, DownsampleDepthMaterial, 0);

#endif

            depthCommandBuffer.SetGlobalFloat(WMS._DownsampleDepthScale, 2.0f);
            depthCommandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, HalfDepthBuffer, DownsampleDepthMaterial, 1);
            depthCommandBuffer.SetGlobalFloat(WMS._DownsampleDepthScale, 4.0f);
            depthCommandBuffer.Blit(HalfDepthBuffer, QuarterDepthBuffer, DownsampleDepthMaterial, 2);
            depthCommandBuffer.SetGlobalFloat(WMS._DownsampleDepthScale, 8.0f);
            depthCommandBuffer.Blit(QuarterDepthBuffer, EighthDepthBuffer, DownsampleDepthMaterial, 3);
            depthCommandBuffer.SetGlobalFloat(WMS._DownsampleDepthScale, 16.0f);
            depthCommandBuffer.Blit(EighthDepthBuffer, SixteenthDepthBuffer, DownsampleDepthMaterial, 4);
            if (deferred)
            {
                camera.AddCommandBuffer(CameraEvent.BeforeReflections, depthCommandBuffer);
            }
            else
            {
                camera.AddCommandBuffer(CameraEvent.AfterDepthTexture, depthCommandBuffer);
            }
        }

        private RenderTargetIdentifier UpdateDepthDownsampler(ref RenderTexture tex, int scale)
        {
            RenderTextureDescriptor desc = WeatherMakerFullScreenEffect.GetRenderTextureDescriptor(scale, 0, 1, RenderTextureFormat.RFloat);
            if (tex == null || tex.width != desc.width || tex.height != desc.height)
            {
                WeatherMakerFullScreenEffect.DestroyRenderTexture(ref tex);
                tex = WeatherMakerFullScreenEffect.CreateRenderTexture(desc, FilterMode.Point, TextureWrapMode.Clamp);
                tex.name = "WeatherMakerDepthTexture_" + scale;
            }
            return tex;
        }

        private void UpdateDepthDownsampler()
        {
            if (DownsampleDepthMaterial != null)
            {

#if COPY_FULL_DEPTH_TEXTURE

                DepthBufferId = UpdateDepthDownsampler(ref depthBuffer, DepthBufferId, 1);

#endif

                RenderTexture tmp = HalfDepthBuffer;
                UpdateDepthDownsampler(ref tmp, 2);
                HalfDepthBuffer = tmp;
                tmp = QuarterDepthBuffer;
                 UpdateDepthDownsampler(ref tmp, 4);
                QuarterDepthBuffer = tmp;
                tmp = EighthDepthBuffer;
                 UpdateDepthDownsampler(ref tmp, 8);
                EighthDepthBuffer = tmp;
                tmp = SixteenthDepthBuffer;
                 UpdateDepthDownsampler(ref tmp, 16);
                SixteenthDepthBuffer = tmp;

#if COPY_FULL_DEPTH_TEXTURE

                Shader.SetGlobalTexture(WMS._CameraDepthTextureOne, depthBuffer);

#endif

                Shader.SetGlobalTexture(WMS._CameraDepthTextureHalf, HalfDepthBuffer);
                Shader.SetGlobalTexture(WMS._CameraDepthTextureQuarter, QuarterDepthBuffer);
                Shader.SetGlobalTexture(WMS._CameraDepthTextureEighth, EighthDepthBuffer);
                Shader.SetGlobalTexture(WMS._CameraDepthTextureSixteenth, SixteenthDepthBuffer);
            }
        }

        private void CleanupDepthTextures()
        {
            HalfDepthBuffer = WeatherMakerFullScreenEffect.DestroyRenderTexture(HalfDepthBuffer);
            QuarterDepthBuffer = WeatherMakerFullScreenEffect.DestroyRenderTexture(QuarterDepthBuffer);
            EighthDepthBuffer = WeatherMakerFullScreenEffect.DestroyRenderTexture(EighthDepthBuffer);
            SixteenthDepthBuffer = WeatherMakerFullScreenEffect.DestroyRenderTexture(SixteenthDepthBuffer);
            WeatherMakerFullScreenEffect.ReleaseCommandBuffer(ref depthCommandBuffer);
        }

        private void AttachDepthCommandBuffer(Camera camera)
        {
            if (DownsampleDepthMaterial != null && !WeatherMakerScript.ShouldIgnoreCamera(this, camera, false) && CameraStack < 2)
            {
                CreateAndAddDepthCommandBuffer(camera);
            }
        }

        private void InvokeEvents(Camera camera, List<KeyValuePair<System.Action<Camera>, MonoBehaviour>> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Value == null)
                {
                    list.RemoveAt(i);
                }
                else if (list[i].Value.enabled)
                {
                    list[i].Key(camera);
                }
            }
        }

        private void CameraPreCull(Camera camera)
        {
            // avoid infinite loop
            if (cameraStack.Contains(camera) || WeatherMakerScript.ShouldIgnoreCamera(this, camera, false))
            {
                return;
            }
            else if (WeatherMakerScript.Instance.AllowCameras != null && !WeatherMakerScript.Instance.AllowCameras.Contains(camera))
            {
                // add to the allow camera list if not already in it
                WeatherMakerScript.Instance.AllowCameras.Add(camera);
            }

            cameraStack.Add(camera);
            InvokeEvents(camera, preCullEvents);
        }

        private void CameraPreRender(Camera camera)
        {
            if (cameraStack.Contains(camera))
            {
                SetupCommandBufferForCamera(camera);
                AttachDepthCommandBuffer(camera);
                CreateAfterForwardOpaqueCommandBuffer(camera);
                InvokeEvents(camera, preRenderEvents);
            }
        }

        private void CameraPostRender(Camera camera)
        {
            if (cameraStack.Contains(camera))
            {
                cameraStack.Remove(camera);
                InvokeEvents(camera, postRenderEvents);
            }
        }

        private static WeatherMakerCommandBufferManagerScript instance;
        /// <summary>
        /// Shared instance of weather maker manager script
        /// </summary>
        public static WeatherMakerCommandBufferManagerScript Instance
        {
            get { return WeatherMakerScript.FindOrCreateInstance<WeatherMakerCommandBufferManagerScript>(ref instance, true); }
        }
    }
}