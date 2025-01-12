using System;
using System.IO;
using UnityEngine;
using RotaryHeart.Lib.SerializableDictionary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using GraphProcessor;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = System.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FernNPRCore.SDNodeGraph
{
    public class SDTextureHandle
    {
        private static Texture2D _refreshIcon;

        public static Texture2D RefreshIcon
        {
            get
            {
                if (_refreshIcon == null)
                {
                    _refreshIcon = Resources.Load<Texture2D>("Icon/refresh");
                }

                return _refreshIcon;
            }
        }

        private static Texture2D _saveIcon;

        public static Texture2D SaveIcon
        {
            get
            {
                if (_saveIcon == null)
                {
                    _saveIcon = Resources.Load<Texture2D>("Icon/save");
                }

                return _saveIcon;
            }
        }
        
        static Texture2D _assetLogo;
        public static Texture2D AssetLogo => _assetLogo == null ? _assetLogo = Resources.Load<Texture2D>("assetLogo") : _assetLogo;

        private static Texture2D _openFolderIcon;

        public static Texture2D OpenFolderIcon
        {
            get
            {
                if (_refreshIcon == null)
                {
                    _refreshIcon = Resources.Load<Texture2D>("Icons/openfolder");
                }

                return _refreshIcon;
            }
        }
    }

    public class SDUtil
    {
        public static readonly float defaultNodeWidth = 200f;
        public static readonly float previewNodeWidth = 256f;
        public static readonly float operatorNodeWidth = 100f;
        public static readonly float smallNodeWidth = 150f;

        public static int[] ScaleList = new int[] { 25, 50, 75, 100 };
        private const string LOG = "Fern SD Graph: ";

        public static readonly string texture2DPrefix = "_2D";
        public static readonly string texture3DPrefix = "_3D";
        public static readonly string textureCubePrefix = "_Cube";

        public static readonly Dictionary<TextureDimension, string> shaderPropertiesDimensionSuffix =
            new Dictionary<TextureDimension, string>
            {
                { TextureDimension.Tex2D, texture2DPrefix },
                { TextureDimension.Tex3D, texture3DPrefix },
                { TextureDimension.Cube, textureCubePrefix },
            };

        public static Vector2 GetMainGameViewSize()
        {
            System.Type T = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            System.Reflection.MethodInfo GetSizeOfMainGameView = T.GetMethod("GetSizeOfMainGameView",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            System.Object Res = GetSizeOfMainGameView.Invoke(null, null);
            return (Vector2)Res;
        }

        public static long GenerateRandomLong(long min, long max)
        {
            byte[] buf = new byte[8];
            new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);
            return (Math.Abs(longRand % (max - min)) + min);
        }

        public static Texture2D TextureToTexture2D(Texture inputTexture)
        {
            // 创建一个新的Texture2D对象
            Texture2D outputTexture =
                new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBA32, false);

            // 创建一个临时的RenderTexture对象
            RenderTexture tempRenderTexture = RenderTexture.GetTemporary(inputTexture.width, inputTexture.height, 0,
                RenderTextureFormat.Default);

            // 将原始Texture对象渲染到临时的RenderTexture中
            Graphics.Blit(inputTexture, tempRenderTexture);

            // 将临时的RenderTexture对象读取到新的Texture2D对象中
            RenderTexture.active = tempRenderTexture;
            outputTexture.ReadPixels(new Rect(0, 0, inputTexture.width, inputTexture.height), 0, 0);
            outputTexture.Apply();

            // 释放临时的RenderTexture对象
            RenderTexture.ReleaseTemporary(tempRenderTexture);

            // 返回新的Texture2D对象
            return outputTexture;
        }
        
        public static void SetupDimensionKeyword(Material material, TextureDimension dimension)
        {
            foreach (var keyword in material.shaderKeywords.Where(s => s.ToLower().Contains("crt")))
                material.DisableKeyword(keyword);

            switch (dimension)
            {
                case TextureDimension.Tex2D:
                    material.EnableKeyword("CRT_2D");
                    break;
                case TextureDimension.Tex2DArray:
                    material.EnableKeyword("CRT_2D_ARRAY");
                    break;
                case TextureDimension.Tex3D:
                    material.EnableKeyword("CRT_3D");
                    break;
                case TextureDimension.Cube:
                    material.EnableKeyword("CRT_CUBE");
                    break;
                default:
                    break;
            }
        }
        
        static int textureDimensionShaderId = Shader.PropertyToID("_TextureDimension");
        public static void SetupComputeTextureDimension(CommandBuffer cmd, ComputeShader computeShader, TextureDimension dimension)
        {
            cmd.SetComputeFloatParam(computeShader, textureDimensionShaderId, (int)dimension);
        }

        public static void SetToNone(string assetPath)
        {
            assetPath = GetAssetPath(assetPath);
#if UNITY_EDITOR
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Log(assetPath);
            if (importer != null)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;
                AssetDatabase.Refresh();
            }
#endif
        }

        public static void SetTextureWithDimension(Material material, string propertyName, Texture texture)
        {
            if (shaderPropertiesDimensionSuffix.TryGetValue(texture.dimension, out var suffix))
            {
#if UNITY_EDITOR
                int id = material.shader.FindPropertyIndex(propertyName + suffix);

                if (id == -1)
                    return;
                if (material.shader.GetPropertyTextureDimension(id) == texture.dimension)
#endif
                {
                    material.SetTexture(propertyName + suffix, texture);
                }
            }
        }
        
        public static void SetTextureWithDimension(CommandBuffer cmd, ComputeShader compute, int kernelIndex, string propertyName, Texture texture)
        {
            foreach (var dim in shaderPropertiesDimensionSuffix)
            {
                if (dim.Key == texture.dimension)
                    cmd.SetComputeTextureParam(compute, kernelIndex, propertyName + dim.Value, texture);
                else // We still need to bind something to the other texture dimension to avoid errors in the console
                    cmd.SetComputeTextureParam(compute, kernelIndex, propertyName + dim.Value, TextureUtils.GetBlackTexture(dim.Key));
            }
        }

        public static void Log(string log, bool isDebug = true)
        {
            if (!isDebug) return;
            Debug.Log($"{LOG}{log}");
        }

        public static void LogWarning(string log, bool isDebug = true)
        {
            if (!isDebug) return;
            Debug.LogWarning($"{LOG}{log}");
        }

        public static void LogError(string log, bool isDebug = true)
        {
            if (!isDebug) return;
            Debug.LogError($"{LOG}{log}");
        }

        
        public static string GetAssetPath(string absolutePath)
        {
            string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
            return relativePath;
        }

        public static void SaveAsLinearPNG(Texture2D texture, string filePath)
        {
            // 将Texture2D转换为伽马空间
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = pixels[i].gamma;
            }

            Texture2D gammaTexture = new Texture2D(texture.width, texture.height);
            gammaTexture.SetPixels(pixels);
            gammaTexture.Apply();

            // 将伽马空间的Texture2D编码为PNG
            byte[] pngData = gammaTexture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);
        }
        
        public static void SaveAsLinearPNG(Texture texture, string filePath)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, renderTexture);

            Texture2D texture2d = new Texture2D(texture.width, texture.height);
            texture2d.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            
            // 将Texture2D转换为伽马空间
            Color[] pixels = texture2d.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = pixels[i].gamma;
            }

            Texture2D gammaTexture = new Texture2D(texture.width, texture.height);
            gammaTexture.SetPixels(pixels);
            gammaTexture.Apply();

            // 将伽马空间的Texture2D编码为PNG
            byte[] pngData = gammaTexture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);
            RenderTexture.ReleaseTemporary(renderTexture);
            UnityEngine.Object.DestroyImmediate(texture2d);
        }

        static Material _dummyCustomRenderTextureMaterial;

        public static Material dummyCustomRenderTextureMaterial
        {
            get
            {
                if (_dummyCustomRenderTextureMaterial == null)
                {
                    var shader = Shader.Find("Hidden/CustomRenderTextureMissingMaterial");

                    if (shader != null)
                        _dummyCustomRenderTextureMaterial = new Material(shader);
                }

                return _dummyCustomRenderTextureMaterial;
            }
        }
    }

    public static class NetAuthorizationUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetRequestAuthorization(this HttpWebRequest request)
        {
            // add auth-header to request
            if (SDGraphResource.SdGraphDataHandle.UseAuth && !SDGraphResource.SdGraphDataHandle.Username.Equals("") &&
                !SDGraphResource.SdGraphDataHandle.Password.Equals(""))
            {
                request.PreAuthenticate = true;
                byte[] bytesToEncode = Encoding.UTF8.GetBytes(SDGraphResource.SdGraphDataHandle.Username + ":" +
                                                              SDGraphResource.SdGraphDataHandle.Password);
                string encodedCredentials = Convert.ToBase64String(bytesToEncode);
                request.Headers.Add("Authorization", "Basic " + encodedCredentials);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetRequestAuthorization(this UnityWebRequest request)
        {
            if (SDGraphResource.SdGraphDataHandle.UseAuth && !SDGraphResource.SdGraphDataHandle.Username.Equals("") &&
                !SDGraphResource.SdGraphDataHandle.Password.Equals(""))
            {
                Debug.Log("Using API key to authenticate");
                byte[] bytesToEncode = Encoding.UTF8.GetBytes(SDGraphResource.SdGraphDataHandle.Username + ":" +
                                                              SDGraphResource.SdGraphDataHandle.Password);
                string encodedCredentials = Convert.ToBase64String(bytesToEncode);
                request.SetRequestHeader("Authorization", "Basic " + encodedCredentials);
            }
        }
    }

    public static class SkinUnit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color SkinColor(this Color color)
        {
            if (EditorGUIUtility.isProSkin)
            {
                color.r *= 1.5f;
                color.g *= 1.5f;
                color.b *= 1.5f;
            }

            return color;
        }
    }

    public static class TimeUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Seconds_To_HMS(this long totalTime)
        {
            string date = string.Empty;
            var seconds = (int)(totalTime % 60);
            var minutes = (int)(totalTime / 60) % 60;
            var hours = (int)(totalTime / 3600) % 60;
            if (hours > 0)
            {
                date += $"{hours:00}";
                date += ":";
            }

            date += $"{minutes:00}";
            date += ":";
            date += $"{seconds:00}";
            return date;
        }
    }

    /// <summary>
    /// Data structure to easily serialize the parameters to send
    /// to the Stable Diffusion server when generating an image via Txt2Img.
    /// </summary>
    class SDParamsInTxt2ImgContronlNet
    {
        public bool enable_hr = false;
        public float denoising_strength = 0.75f;
        public int firstphase_width = 0;
        public int firstphase_height = 0;
        public float hr_scale = 2;
        public string hr_upscaler = "";
        public int hr_second_pass_steps = 0;
        public int hr_resize_x = 0;
        public int hr_resize_y = 0;
        public string prompt = "";
        public string[] styles = { "" };
        public long seed = -1;
        public long subseed = -1;
        public float subseed_strength = 0;
        public int seed_resize_from_h = -1;
        public int seed_resize_from_w = -1;
        public string sampler_name = "Euler a";
        public int batch_size = 1;
        public int n_iter = 1;
        public int steps = 50;
        public float cfg_scale = 7;
        public int width = 512;
        public int height = 512;
        public bool restore_faces = false;
        public bool tiling = false;
        public string negative_prompt = "";
        public float eta = 0;
        public float s_churn = 0;
        public float s_tmax = 0;
        public float s_tmin = 0;
        public float s_noise = 1;
        public bool override_settings_restore_afterwards = true;
        public string sampler_index = "Euler";
    }

    class SDParamsInTxt2Img
    {
        public string prompt = "";
        public string negative_prompt = "";
        public string[] styles = { };
        public long seed = -1;
        public long subseed = -1;
        public float subseed_strength = 0;
        public int seed_resize_from_h = -1;
        public int seed_resize_from_w = -1;
        public string sampler_name = "Euler a";
        public int batch_size = 1;
        public int n_iter = 1;
        public int steps = 50;
        public float cfg_scale = 7;
        public int width = 512;
        public int height = 512;
        public bool restore_faces = false;
        public bool tiling = false;
        public bool do_not_save_samples = false;
        public bool do_not_save_grid = false;
        public float eta = 0;
        public float denoising_strength = 0.75f;
        public float s_min_uncond = 0.75f;
        public float s_churn = 0;
        public float s_tmax = 0;
        public float s_tmin = 0;
        public float s_noise = 1;
        // "override_settings": {},
        public bool override_settings_restore_afterwards = true;
        public string refiner_checkpoint = "";
        //public float refiner_switch_at = 0;
        public bool disable_extra_networks = false;
        //"comments": {},
        public bool enable_hr = false;
        public int firstphase_width = 0;
        public int firstphase_height = 0;
        public float hr_scale = 2;
        public string hr_upscaler = "";
        public int hr_second_pass_steps = 0;
        public int hr_resize_x = 0;
        public int hr_resize_y = 0;
        public string hr_checkpoint_name = "";
        public string hr_sampler_name = "";
        public string hr_prompt = "";
        public string hr_negative_prompt = "";
        public string sampler_index = "Euler";
    }

    class ControlNetDetect
    {
        public string controlnet_module = "none";
        public string[] controlnet_input_images = new[] { "" };
        public int controlnet_processor_res = 64;
        public int controlnet_threshold_a = 64;
        public int controlnet_threshold_b = 64;
    }

    /// <summary>
    /// Data structure to easily deserialize the data returned
    /// by the Stable Diffusion server after generating an image via Txt2Img.
    /// </summary>
    class SDParamsOutTxt2Img
    {
        public bool enable_hr = false;
        public float denoising_strength = 0;
        public int firstphase_width = 0;
        public int firstphase_height = 0;
        public float hr_scale = 2;
        public string hr_upscaler = "";
        public int hr_second_pass_steps = 0;
        public int hr_resize_x = 0;
        public int hr_resize_y = 0;
        public string prompt = "";
        public string[] styles = { "" };
        public long seed = -1;
        public long subseed = -1;
        public float subseed_strength = 0;
        public int seed_resize_from_h = -1;
        public int seed_resize_from_w = -1;
        public string sampler_name = "Euler a";
        public int batch_size = 1;
        public int n_iter = 1;
        public int steps = 50;
        public float cfg_scale = 7;
        public int width = 512;
        public int height = 512;
        public bool restore_faces = false;
        public bool tiling = false;
        public string negative_prompt = "";
        public float eta = 0;
        public float s_churn = 0;
        public float s_tmax = 0;
        public float s_tmin = 0;
        public float s_noise = 1;
        public SettingsOveride override_settings;
        public bool override_settings_restore_afterwards = true;
        public object[] script_args = { };
        public string sampler_index = "Euler";
        public string script_name = null;

        public class SettingsOveride
        {
        }
    }

    class SDParamsInImg2Img
    {
        public string[] init_images = { "" };
        public int resize_mode = 0;

        public float denoising_strength = 0.5f;

        //public string mask = null; 
        public int mask_blur = 4;
        public int inpainting_fill = 1;
        public bool inpaint_full_res = true;
        public int inpaint_full_res_padding = 0;
        public int inpainting_mask_invert = 0;
        public int initial_noise_multiplier = 1;
        public string prompt = "";
        public string[] styles = { "" };
        public long seed = -1;
        public long subseed = -1;
        public int subseed_strength = 0;
        public int seed_resize_from_h = -1;
        public int seed_resize_from_w = -1;
        public string sampler_name = "Euler a";
        public int batch_size = 1;
        public int n_iter = 1;
        public int steps = 50;
        public float cfg_scale = 7;
        public int width = 512;
        public int height = 512;
        public bool restore_faces = false;
        public bool tiling = false;
        public string negative_prompt = "";
        public float eta = 0;
        public float s_churn = 0;
        public float s_tmax = 0;
        public float s_tmin = 0;
        public float s_noise = 1;
        public SettingsOveride override_settings;
        public bool override_settings_restore_afterwards = true;
        public object[] script_args = { };
        public string sampler_index = "Euler";

        public bool include_init_images = false;

        public string script_name = null;
        public string mask = null;
        public class SettingsOveride
        {
        }
    }

    /// <summary>
    /// Data structure to easily deserialize the data returned
    /// by the Stable Diffusion server after generating an image via Img2Img.
    /// </summary>
    class SDParamsOutImg2Img
    {
        public string[] init_images = { "" };
        public float resize_mode = 0;
        public float denoising_strength = 0.75f;
        public string mask = null;
        public float mask_blur = 10;
        public float inpainting_fill = 0;
        public bool inpaint_full_res = true;
        public float inpaint_full_res_padding = 32;
        public float inpainting_mask_invert = 0;
        public float initial_noise_multiplier = 0;
        public string prompt = "";
        public string[] styles = { "" };
        public long seed = -1;
        public long subseed = -1;
        public float subseed_strength = 0;
        public float seed_resize_from_h = -1;
        public float seed_resize_from_w = -1;
        public string sampler_name = "";
        public float batch_size = 1;
        public float n_iter = 1;
        public int steps = 50;
        public float cfg_scale = 7;
        public int width = 512;
        public int height = 512;
        public bool restore_faces = false;
        public bool tiling = false;
        public string negative_prompt = "";
        public float eta = 0;
        public float s_churn = 0;
        public float s_tmax = 0;
        public float s_tmin = 0;
        public float s_noise = 1;
        public SettingsOveride override_settings;
        public bool override_settings_restore_afterwards = true;
        public object[] script_args = { };
        public string sampler_index = "Euler";
        public bool include_init_images = false;
        public string script_name = null;

        public class SettingsOveride
        {
        }
    }

    /// <summary>
    /// Data structure to help serialize into a JSON the model to be used by Stable Diffusion.
    /// This is to send along a Set Option API request to the server.
    /// </summary>
    class SDOption
    {
        public string sd_model_checkpoint = "";
    }


    public class SDResponseState
    {
        public bool skipped;
        public bool interrupted;
        public string job;
        public int job_count;
        public string job_timestamp;
        public int job_no;
        public int sampling_step;
        public int sampling_steps;
    }

    /// <summary>
    /// SD image generate Progress data
    /// </summary>
    public class SDResponseProgress
    {
        public float progress;
        public float eta_relative;
        public string info;
        public SDResponseState state;
        public string current_image;
        public string textinfo;
    }

    /// <summary>
    /// Data structure to easily deserialize the JSON response returned
    /// by the Stable Diffusion server after generating an image via Txt2Img.
    ///
    /// It will contain the generated images (in Ascii Byte64 format) and
    /// the parameters used by Stable Diffusion.
    /// 
    /// Note that the out parameters returned should be almost identical to the in
    /// parameters that you have submitted to the server for image generation, 
    /// to the exception of the seed which will contain the value of the seed used 
    /// for the generation if you have used -1 for value (random).
    /// </summary>
    class SDResponseTxt2Img
    {
        public string[] images;
        public SDParamsOutTxt2Img parameters;
        public string info;
    }

    /// <summary>
    /// Data structure to easily deserialize the JSON response returned
    /// by the Stable Diffusion server after generating an image via Img2Img.
    ///
    /// It will contain the generated images (in Ascii Byte64 format) and
    /// the parameters used by Stable Diffusion.
    /// 
    /// Note that the out parameters returned should be almost identical to the in
    /// parameters that you have submitted to the server for image generation, 
    /// to the exception of the seed which will contain the value of the seed used 
    /// for the generation if you have used -1 for value (random).
    /// </summary>
    class SDResponseImg2Img
    {
        public string[] images;
        public SDParamsOutImg2Img parameters;
        public string info;
    }

    public enum ResizeMode
    {
        JustResize = 0,
        ScaleToFit_InnerFit = 1,
        Envelope_OuterFit = 2
    }

    public enum ControlMode
    {
        Balanced = 0,
        My_prompt_is_more_important = 1,
        ControlNet_is_more_important = 2
    }

    public class ControlNetData
    {
        public string input_image = "";
        public string mask = null;
        public string module = "none";
        public string model;
        public float weight = 1;
        public int resize_mode = 1;
        public bool lowvram = false;
        public int processor_res = 64;
        public int threshold_a = 64;
        public int threshold_b = 64;
        public float guidance_start = 0.0f;
        public float guidance_end = 1.0f;
        public float guidance = 1f;
        public int control_mode = 0;
    }

    public class ControlNetDataArgs
    {
        public ControlNetData[] args;
    }
    

    public class SCRIPT
    {
        public string name;
        public object[] args;
    }
    
    class ExtraSingleImageParam
    {
        public string image = "";
        public int resize_mode = 0;
        public bool show_extras_results = true;
        public float gfpgan_visibility = 0;
        public float codeformer_visibility = 0;
        public float codeformer_weight = 0;
        public float upscaling_resize = 2;
        public int upscaling_resize_w = 512;
        public int upscaling_resize_h = 512;
        public bool upscaling_crop = true;
        public string upscaler_1 = "None";
        public string upscaler_2 = "None";
        public float extras_upscaler_2_visibility = 0;
        public bool upscale_first = false;
    }
    
    class ExtraSingleImageResponse
    {
        public string html_info = "";
        public string image = "";
    }

    public class SDModel
    {
        public string title;
        public string model_name;
        public string hash;
        public string sha256;
        public string filename;
        public string config;
    }

    public class SDLoraModel
    {
        public string name;

        public string path;
        //public string prompt;
    }

    public class SDDataDir
    {
        public string data_dir;
        public string lora_dir;
    }
    
    public class SDUpscalerModel
    {
        public string name;
        public string model_name;
        public string model_path;
        public string model_url;
        public string scale;
    }


    public class ControlNetModel
    {
        public string[] model_list;
    }

    public class ControlNetMoudle
    {
        public string[] module_list;
    }

    [Serializable]
    public class Prompt
    {
        public string positive;
        public string negative;
    }

    [System.Serializable]
    public class StringStringDic : SerializableDictionaryBase<string, string>
    {
    }

    [Serializable]
    public class HiresUpscaler
    {
        public float denoising_strength = 0.75f;
        public float hr_scale = 2;
        public string hr_upscaler = "";
        public int hr_second_pass_steps = 1;
        public int hr_resize_x = 0;
        public int hr_resize_y = 0;
    }
}