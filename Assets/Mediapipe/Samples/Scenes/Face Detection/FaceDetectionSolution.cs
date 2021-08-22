using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.FaceDetection {
  public class FaceDetectionSolution : Solution {
    [SerializeField] RawImage screen;
    [SerializeField] DetectionListAnnotationController annotationController;
    [SerializeField] FaceDetectionGraph graphRunner;
    [SerializeField] TextureFramePool textureFramePool;

    Coroutine coroutine;

    public RunningMode runningMode;
    public FaceDetectionGraph.ModelType modelType {
      get { return graphRunner.modelType; }
      set { graphRunner.modelType = value; }
    }

    public override void Play() {
      base.Play();
      graphRunner.Initialize();

      if (coroutine != null) {
        StopCoroutine(coroutine);
      }
      coroutine = StartCoroutine(Run());
    }

    public override void Pause() {
      base.Pause();
      ImageSourceProvider.imageSource.Pause();
      ClearAnnotations();
    }

    public override void Resume() {
      base.Resume();
      StartCoroutine(ImageSourceProvider.imageSource.Resume());
    }

    public override void Stop() {
      base.Stop();
      StopCoroutine(coroutine);
      ImageSourceProvider.imageSource.Stop();
      graphRunner.Stop();
      ClearAnnotations();
    }

    IEnumerator Run() {
      var imageSource = ImageSourceProvider.imageSource;

      yield return imageSource.Play();

      if (!imageSource.isPrepared) {
        Debug.LogError("Failed to start ImageSource, exiting...");
        yield break;
      }

      screen.rectTransform.sizeDelta = new Vector2(imageSource.textureWidth, imageSource.textureHeight);
      screen.texture = imageSource.GetCurrentTexture();

      var graphRunner = gameObject.GetComponent<FaceDetectionGraph>();

      Debug.Log($"Model Selection: {modelType}");
      Debug.Log($"Running Mode: {runningMode}");

      if (runningMode == RunningMode.Async) {
        graphRunner.OnFacesDetected.AddListener(OnFacesDetected);
        graphRunner.StartRunAsync(imageSource).AssertOk();
      } else {
        graphRunner.StartRun(imageSource).AssertOk();
      }

      // Decide which TextureFormat to use
      if (graphRunner.configType == GraphRunner.ConfigType.OpenGLES) {
        // Use BGRA32 when the input packet is GpuBuffer
        textureFramePool.ResizeTexture(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.BGRA32);
      } else {
        // Use RGBA32 when the input packet is ImageFrame
        textureFramePool.ResizeTexture(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32);
      }

      annotationController.isMirrored = imageSource.isMirrored;

      while (true) {
        yield return new WaitWhile(() => isPaused);

        var textureFrameRequest = textureFramePool.WaitForNextTextureFrame();
        yield return textureFrameRequest;
        var textureFrame = textureFrameRequest.result;

        var currentTexture = imageSource.GetCurrentTexture();

        // Copy currentTexture to textureFrame's texture
        if (graphRunner.configType == GraphRunner.ConfigType.OpenGLES) {
          textureFrame.ReadTextureFromOnGPU(currentTexture);
        } else {
          var textureType = currentTexture.GetType();

          if (textureType == typeof(WebCamTexture)) {
            textureFrame.ReadTextureFromOnCPU((WebCamTexture)currentTexture);
          } else if (textureType == typeof(Texture2D)) {
            textureFrame.ReadTextureFromOnCPU((Texture2D)currentTexture);
          } else {
            textureFrame.ReadTextureFromOnCPU(currentTexture);
          }
        }

        graphRunner.AddTextureFrameToInputStream(textureFrame).AssertOk();

        if (runningMode == RunningMode.Sync) {
          // When running synchronously, wait for the outputs here (blocks the main thread).
          var detections = graphRunner.FetchNextDetections();
          annotationController.Draw(detections);
        }

        yield return new WaitForEndOfFrame();
      }
    }

    void ClearAnnotations() {
      annotationController.Draw(null);
    }

    void OnFacesDetected(List<Detection> detections) {
      if (!isPaused) {
        annotationController.Draw(detections);
      }
    }
  }
}
