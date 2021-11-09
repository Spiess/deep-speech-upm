using System;
using UnityEngine.Events;

namespace DeepSpeechUPM
{
  /// <summary>
  /// DeepSpeech event providing a final speech-to-text result.
  /// </summary>
  [Serializable]
  public class DeepSpeechResultEvent : UnityEvent<string>
  {
  }

  /// <summary>
  /// DeepSpeech event providing a speech-to-text prediction based on an ongoing continuous stream in progress.
  /// </summary>
  [Serializable]
  public class DeepSpeechPredictionEvent : UnityEvent<string>
  {
  }
}