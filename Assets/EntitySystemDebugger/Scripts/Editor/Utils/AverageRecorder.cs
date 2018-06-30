using UnityEngine;
using UnityEngine.Profiling;

namespace EntitySystemDebugger.Editor.Utils
{
    public class AverageRecorder
    {
        private readonly Recorder recorder;
        private int frameCount;
        private int totalNanoseconds;
        private float nextRecord;
        public CircularBuffer<float> buffer;
        public float lastReading;
        public float max;
        public float average;

        public AverageRecorder (Recorder recorder)
        {
            buffer = new CircularBuffer<float> (100);
            for (int i = 0; i < 100; i++)
            {
                buffer.PushBack (0);
            }
            this.recorder = recorder;
        }

        public void Update ()
        {
            ++frameCount;
            totalNanoseconds += (int) recorder.elapsedNanoseconds;
            if (Time.time > nextRecord)
            {
                nextRecord = Time.time + 0.05f;
                RecordSeconds ();
            }
        }

        public void RecordSeconds ()
        {
            if (frameCount > 0)
            {
                lastReading = (totalNanoseconds / 1e6f) / frameCount;
                frameCount = totalNanoseconds = 0;
            }
            buffer.PushBack (lastReading);

            max = 0;
            float total = 0;
            for (int i = 0; i < 100; i++)
            {
                total += buffer[i];
                if (buffer[i] > max)
                {
                    max = buffer[i];
                }
            }
            average = total / 100;
        }
    }
}