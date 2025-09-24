using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KimiAppNative.VoiceServices
{
    /// <summary>
    /// Manages voice input and output capabilities
    /// </summary>
    public class VoiceService : IDisposable
    {
        private SpeechRecognitionEngine? _recognizer;
        private SpeechSynthesizer? _synthesizer;
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _waveWriter;
        private bool _isRecording;
        private bool _isListening;
        private readonly List<VoiceCommand> _voiceCommands;
        private VoiceSettings _settings;

        public bool IsListening => _isListening;
        public bool IsRecording => _isRecording;
        public VoiceSettings Settings => _settings;

        public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
        public event EventHandler<VoiceCommandEventArgs>? CommandRecognized;
        public event EventHandler<AudioLevelEventArgs>? AudioLevelChanged;
        public event EventHandler<string>? PartialResultReceived;
        public event EventHandler<Exception>? ErrorOccurred;

        public VoiceService()
        {
            _voiceCommands = new List<VoiceCommand>();
            _settings = new VoiceSettings();
            InitializeVoiceComponents();
        }

        /// <summary>
        /// Initializes voice components
        /// </summary>
        private void InitializeVoiceComponents()
        {
            try
            {
                // Initialize speech synthesizer
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SetOutputToDefaultAudioDevice();
                ConfigureSynthesizer();

                // Initialize speech recognizer
                _recognizer = new SpeechRecognitionEngine();
                _recognizer.SetInputToDefaultAudioDevice();
                ConfigureRecognizer();

                // Initialize audio recording
                InitializeAudioRecording();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Configures the speech synthesizer
        /// </summary>
        private void ConfigureSynthesizer()
        {
            if (_synthesizer == null) return;

            _synthesizer.Rate = _settings.SpeechRate;
            _synthesizer.Volume = _settings.SpeechVolume;

            // Select voice
            var voices = _synthesizer.GetInstalledVoices();
            var selectedVoice = voices.FirstOrDefault(v => v.VoiceInfo.Name == _settings.VoiceName);
            if (selectedVoice != null)
            {
                _synthesizer.SelectVoice(selectedVoice.VoiceInfo.Name);
            }
        }

        /// <summary>
        /// Configures the speech recognizer
        /// </summary>
        private void ConfigureRecognizer()
        {
            if (_recognizer == null) return;

            // Set up grammar for general dictation
            var dictationGrammar = new DictationGrammar();
            _recognizer.LoadGrammar(dictationGrammar);

            // Load command grammar
            LoadCommandGrammar();

            // Wire up events
            _recognizer.SpeechRecognized += OnSpeechRecognized;
            _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
            _recognizer.SpeechHypothesized += OnSpeechHypothesized;
        }

        /// <summary>
        /// Initializes audio recording capabilities
        /// </summary>
        private void InitializeAudioRecording()
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1), // 16kHz, Mono
                BufferMilliseconds = 50
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;
        }

        /// <summary>
        /// Starts listening for voice input
        /// </summary>
        public async Task StartListeningAsync(ListeningMode mode = ListeningMode.Continuous)
        {
            if (_isListening) return;

            try
            {
                _isListening = true;

                if (mode == ListeningMode.Continuous)
                {
                    _recognizer?.RecognizeAsync(RecognizeMode.Multiple);
                }
                else
                {
                    _recognizer?.RecognizeAsync(RecognizeMode.Single);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _isListening = false;
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Stops listening for voice input
        /// </summary>
        public void StopListening()
        {
            if (!_isListening) return;

            _isListening = false;
            _recognizer?.RecognizeAsyncStop();
        }

        /// <summary>
        /// Speaks the given text
        /// </summary>
        public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            if (_synthesizer == null || string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                var tcs = new TaskCompletionSource<bool>();

                _synthesizer.SpeakCompleted += (s, e) => tcs.TrySetResult(true);

                using (cancellationToken.Register(() =>
                {
                    _synthesizer.SpeakAsyncCancelAll();
                    tcs.TrySetCanceled();
                }))
                {
                    _synthesizer.SpeakAsync(text);
                    await tcs.Task;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Speaks text with SSML markup
        /// </summary>
        public async Task SpeakSsmlAsync(string ssml, CancellationToken cancellationToken = default)
        {
            if (_synthesizer == null || string.IsNullOrWhiteSpace(ssml))
                return;

            try
            {
                var tcs = new TaskCompletionSource<bool>();

                _synthesizer.SpeakCompleted += (s, e) => tcs.TrySetResult(true);

                using (cancellationToken.Register(() =>
                {
                    _synthesizer.SpeakAsyncCancelAll();
                    tcs.TrySetCanceled();
                }))
                {
                    _synthesizer.SpeakSsmlAsync(ssml);
                    await tcs.Task;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Starts recording audio to file
        /// </summary>
        public void StartRecording(string filePath)
        {
            if (_isRecording) return;

            try
            {
                _waveWriter = new WaveFileWriter(filePath, _waveIn!.WaveFormat);
                _waveIn?.StartRecording();
                _isRecording = true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Stops recording audio
        /// </summary>
        public string? StopRecording()
        {
            if (!_isRecording) return null;

            try
            {
                _waveIn?.StopRecording();
                var filePath = _waveWriter?.Filename;
                _waveWriter?.Dispose();
                _waveWriter = null;
                _isRecording = false;
                return filePath;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return null;
            }
        }

        /// <summary>
        /// Registers a voice command
        /// </summary>
        public void RegisterCommand(VoiceCommand command)
        {
            _voiceCommands.Add(command);
            UpdateCommandGrammar();
        }

        /// <summary>
        /// Unregisters a voice command
        /// </summary>
        public void UnregisterCommand(string commandId)
        {
            _voiceCommands.RemoveAll(c => c.Id == commandId);
            UpdateCommandGrammar();
        }

        /// <summary>
        /// Updates voice settings
        /// </summary>
        public void UpdateSettings(VoiceSettings settings)
        {
            _settings = settings;
            ConfigureSynthesizer();
            
            if (_settings.EnableVoiceActivation)
            {
                StartVoiceActivation();
            }
            else
            {
                StopVoiceActivation();
            }
        }

        /// <summary>
        /// Gets available voices
        /// </summary>
        public List<VoiceInfo> GetAvailableVoices()
        {
            var voices = new List<VoiceInfo>();
            
            if (_synthesizer != null)
            {
                foreach (var voice in _synthesizer.GetInstalledVoices())
                {
                    voices.Add(new VoiceInfo
                    {
                        Name = voice.VoiceInfo.Name,
                        Culture = voice.VoiceInfo.Culture.Name,
                        Gender = voice.VoiceInfo.Gender.ToString(),
                        Age = voice.VoiceInfo.Age.ToString(),
                        Description = voice.VoiceInfo.Description
                    });
                }
            }
            
            return voices;
        }

        /// <summary>
        /// Converts text to audio file
        /// </summary>
        public async Task<string> TextToAudioFileAsync(string text, string outputPath)
        {
            if (_synthesizer == null || string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Synthesizer not initialized or text is empty");

            try
            {
                _synthesizer.SetOutputToWaveFile(outputPath);
                _synthesizer.Speak(text);
                _synthesizer.SetOutputToDefaultAudioDevice();
                
                return outputPath;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Loads command grammar
        /// </summary>
        private void LoadCommandGrammar()
        {
            if (_recognizer == null || !_voiceCommands.Any()) return;

            var choices = new Choices();
            foreach (var command in _voiceCommands)
            {
                choices.Add(command.Phrases.ToArray());
            }

            var grammarBuilder = new GrammarBuilder(choices);
            var commandGrammar = new Grammar(grammarBuilder)
            {
                Name = "Commands"
            };

            _recognizer.LoadGrammar(commandGrammar);
        }

        /// <summary>
        /// Updates command grammar
        /// </summary>
        private void UpdateCommandGrammar()
        {
            if (_recognizer == null) return;

            // Unload existing command grammar
            var existingGrammar = _recognizer.Grammars
                .FirstOrDefault(g => g.Name == "Commands");
            
            if (existingGrammar != null)
            {
                _recognizer.UnloadGrammar(existingGrammar);
            }

            // Load new grammar
            LoadCommandGrammar();
        }

        /// <summary>
        /// Starts voice activation detection
        /// </summary>
        private void StartVoiceActivation()
        {
            // Implementation for wake word detection
            // This would typically involve a separate always-on recognizer
            // configured to listen for specific activation phrases
        }

        /// <summary>
        /// Stops voice activation detection
        /// </summary>
        private void StopVoiceActivation()
        {
            // Stop wake word detection
        }

        /// <summary>
        /// Handles speech recognized event
        /// </summary>
        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence < _settings.MinConfidence)
                return;

            // Check if it's a command
            var command = _voiceCommands.FirstOrDefault(c =>
                c.Phrases.Any(p => p.Equals(e.Result.Text, StringComparison.OrdinalIgnoreCase)));

            if (command != null)
            {
                CommandRecognized?.Invoke(this, new VoiceCommandEventArgs(command, e.Result.Text));
            }
            else
            {
                SpeechRecognized?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Handles speech rejected event
        /// </summary>
        private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            // Handle rejected speech
        }

        /// <summary>
        /// Handles speech hypothesized event
        /// </summary>
        private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        {
            PartialResultReceived?.Invoke(this, e.Result.Text);
        }

        /// <summary>
        /// Handles audio data available event
        /// </summary>
        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_isRecording && _waveWriter != null)
            {
                _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            }

            // Calculate audio level
            var level = CalculateAudioLevel(e.Buffer, e.BytesRecorded);
            AudioLevelChanged?.Invoke(this, new AudioLevelEventArgs(level));
        }

        /// <summary>
        /// Calculates audio level from buffer
        /// </summary>
        private float CalculateAudioLevel(byte[] buffer, int bytesRecorded)
        {
            float max = 0;
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                float sample32 = sample / 32768f;
                if (sample32 < 0) sample32 = -sample32;
                if (sample32 > max) max = sample32;
            }
            return max;
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            StopListening();
            StopRecording();
            
            _recognizer?.Dispose();
            _synthesizer?.Dispose();
            _waveIn?.Dispose();
            _waveWriter?.Dispose();
        }
    }

    /// <summary>
    /// Voice settings
    /// </summary>
    public class VoiceSettings
    {
        public string VoiceName { get; set; } = string.Empty;
        public int SpeechRate { get; set; } = 0; // -10 to 10
        public int SpeechVolume { get; set; } = 100; // 0 to 100
        public float MinConfidence { get; set; } = 0.6f;
        public bool EnableVoiceActivation { get; set; } = false;
        public string ActivationPhrase { get; set; } = "Hey OhGee";
        public bool EnableSoundEffects { get; set; } = true;
        public bool EnableNoiseSuppression { get; set; } = true;
        public string Language { get; set; } = "en-US";
    }

    /// <summary>
    /// Voice command definition
    /// </summary>
    public class VoiceCommand
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public List<string> Phrases { get; set; } = new();
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Voice information
    /// </summary>
    public class VoiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Culture { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Listening modes
    /// </summary>
    public enum ListeningMode
    {
        Single,
        Continuous
    }

    /// <summary>
    /// Voice command event arguments
    /// </summary>
    public class VoiceCommandEventArgs : EventArgs
    {
        public VoiceCommand Command { get; }
        public string RecognizedText { get; }

        public VoiceCommandEventArgs(VoiceCommand command, string recognizedText)
        {
            Command = command;
            RecognizedText = recognizedText;
        }
    }

    /// <summary>
    /// Audio level event arguments
    /// </summary>
    public class AudioLevelEventArgs : EventArgs
    {
        public float Level { get; }

        public AudioLevelEventArgs(float level)
        {
            Level = level;
        }
    }
}