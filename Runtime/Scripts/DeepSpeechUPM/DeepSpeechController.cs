using System;
using System.Collections.Generic;
using System.Linq;
using DeepSpeechClient.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepSpeechUPM
{
  public class DeepSpeechController : MonoBehaviour
  {
    public InputAction dictateAction;

    /// <summary>
    /// The size in seconds for the microphone recording buffer.
    /// </summary>
    private const int BufferLength = 1;

    private const float BufferRatio = 0.5f;

    private IDeepSpeech _sttClient;
    private int _modelSampleRate;
    private AudioClip _clipBuffer;
    private bool _recording;
    private int _previousPosition;

    private List<float> _buffer = new List<float>();

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
        _buffer.AddRange(data);
        _previousPosition = position;
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
      _buffer.AddRange(data);

      Microphone.End(null);

      // Rescale to short
      var shortData = _buffer.Select(value => (short) (value * short.MaxValue)).ToArray();

      Debug.Log("Running inference....");
      var speechResult = _sttClient.SpeechToText(shortData, Convert.ToUInt32(shortData.Length));
      Debug.Log(speechResult);

      _buffer.Clear();
    }
  }
}