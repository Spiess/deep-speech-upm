using System;
using System.Linq;
using DeepSpeechClient.Interfaces;
using DeepSpeechClient.Models;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepSpeechUPM
{
  /// <summary>
  /// Controller class for continuous stream-based speech-to-text with DeepSpeech.
  /// </summary>
  public class DeepSpeechStreamController : MonoBehaviour
  {
    public InputAction dictateAction;

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

    private void Start()
    {
      _sttClient =
        new DeepSpeechClient.DeepSpeech(Application.dataPath + "/Resources/DeepSpeech/deepspeech-0.9.3-models.pbmm");
      _modelSampleRate = _sttClient.GetModelSampleRate();

      dictateAction.performed += SetDictation;
      dictateAction.canceled += SetDictation;
    }

    private void Update()
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
        _sttClient.FeedAudioContent(_sttStream, shortData, Convert.ToUInt32(shortData.Length));
        var currentPrediction = _sttClient.IntermediateDecode(_sttStream);
        Debug.Log(currentPrediction);
      }
    }

    private void OnEnable()
    {
      dictateAction.Enable();
    }

    private void OnDisable()
    {
      dictateAction.Disable();
    }

    public void SetDictation(InputAction.CallbackContext context)
    {
      if (context.performed)
      {
        StartRecording();
      }
      else
      {
        StopRecording();
      }
    }

    private void StartRecording()
    {
      if (_recording)
      {
        Debug.LogError("Already recording!");
        return;
      }

      _recording = true;
      _previousPosition = 0;
      Debug.Log("Start recording");
      _clipBuffer = Microphone.Start(null, true, BufferLength, _modelSampleRate);

      _sttStream = _sttClient.CreateStream();
    }

    private void StopRecording()
    {
      if (!_recording)
      {
        Debug.LogError("Not recording!");
        return;
      }

      _recording = false;
      Debug.Log("Stop recording");
      var position = Microphone.GetPosition(null);

      var samples = _clipBuffer.samples;
      var sampleDifference = (position - _previousPosition + samples) % samples;
      var data = new float[sampleDifference];

      _clipBuffer.GetData(data, position);

      Microphone.End(null);

      var shortData = data.Select(value => (short) (value * short.MaxValue)).ToArray();
      _sttClient.FeedAudioContent(_sttStream, shortData, Convert.ToUInt32(shortData.Length));
      var speechResult = _sttClient.FinishStream(_sttStream);
      Debug.Log(speechResult);

      _sttStream = null;
    }
  }
}