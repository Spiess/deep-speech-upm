using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeepSpeechClient.Interfaces;
using DeepSpeechClient.Models;
using UnityEngine;

namespace DeepSpeech
{
  /// <summary>
  /// Controller class for continuous stream-based speech-to-text with DeepSpeech.
  /// </summary>
  public class DeepSpeechStreamController : MonoBehaviour
  {
    public DeepSpeechResultEvent onResult;
    public DeepSpeechPredictionEvent onPrediction;

    /// <summary>
    /// The size in seconds for the microphone recording buffer.
    /// </summary>
    private const int BufferLength = 1;

    private const float BufferRatio = 0.5f;

    private IDeepSpeech _sttClient;
    private DeepSpeechStream _sttStream;
    private int _modelSampleRate;
    private AudioClip _clipBuffer;
    private bool _recording;
    private int _previousPosition;

    /// <summary>
    /// Lock to prevent the DeepSpeech client from being used simultaneously and crashing.
    /// </summary>
    private readonly SemaphoreSlim _sttLock = new SemaphoreSlim(1, 1);

    private void Start()
    {
      _sttClient =
        new DeepSpeechClient.DeepSpeech(Application.dataPath + "/Resources/DeepSpeech/deepspeech-0.9.3-models.pbmm");
      _modelSampleRate = _sttClient.GetModelSampleRate();
    }

    private async void Update()
    {
      if (!_recording) return;

      // Calculate buffer ratio
      var position = Microphone.GetPosition(null);
      var samples = _clipBuffer.samples;

      var sampleDifference = (position - _previousPosition + samples) % samples;

      if (sampleDifference > BufferRatio * samples)
      {
        var data = new float[sampleDifference];

        _clipBuffer.GetData(data, position);
        _previousPosition = position;

        var shortData = data.Select(value => (short) (value * short.MaxValue)).ToArray();
        await _sttLock.WaitAsync();
        var currentPrediction = await Task.Run(() =>
        {
          _sttClient.FeedAudioContent(_sttStream, shortData, Convert.ToUInt32(shortData.Length));
          return _sttClient.IntermediateDecode(_sttStream);
        });

        onPrediction.Invoke(currentPrediction);
        _sttLock.Release();
      }
    }

    public void StartDictation()
    {
      if (_recording)
      {
        Debug.LogError("Already recording!");
        return;
      }

      _recording = true;
      _previousPosition = 0;
      _clipBuffer = Microphone.Start(null, true, BufferLength, _modelSampleRate);

      _sttStream = _sttClient.CreateStream();
    }

    public async void StopDictation()
    {
      if (!_recording)
      {
        Debug.LogError("Not recording!");
        return;
      }

      _recording = false;
      var position = Microphone.GetPosition(null);

      var samples = _clipBuffer.samples;
      var sampleDifference = (position - _previousPosition + samples) % samples;
      var data = new float[sampleDifference];

      _clipBuffer.GetData(data, position);

      Microphone.End(null);

      var shortData = data.Select(value => (short) (value * short.MaxValue)).ToArray();

      await _sttLock.WaitAsync();
      var speechResult = await Task.Run(() =>
      {
        if (shortData.Length > 0)
        {
          _sttClient.FeedAudioContent(_sttStream, shortData, Convert.ToUInt32(shortData.Length));
        }

        return _sttClient.FinishStream(_sttStream);
      });
      onResult.Invoke(speechResult);
      _sttLock.Release();

      _sttStream = null;
      _clipBuffer = null;
    }
  }
}