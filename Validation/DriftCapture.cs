using System;
using System.IO;
using UnityEngine;

public static class DriftCapture
{
    public static string Capture(string file, int width = 1280, int height = 720, string screen = null)
    {
        if (screen != null)
        {
            var app = UnityEngine.Object.FindAnyObjectByType<Drift.DriftApplication>();
            if (screen == "Menu") app.EndVoyage();
            else app.Navigate((Drift.ScreenId)Enum.Parse(typeof(Drift.ScreenId), screen));
        }
        var camera = Camera.main;
        var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (camera == null || canvas == null) throw new InvalidOperationException("Start Drift Play Mode first.");
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/Preview", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG()); return path;
        }
        finally
        {
            canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousDistance;
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
            Canvas.ForceUpdateCanvases();
        }
    }
}

