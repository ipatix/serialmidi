using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace serialmidi
{
    /*
     * This class provides a timer with precise absolute timings (although with possible jitter)
     * The idea is precisely scheduling music events without the length of a song drifting too far off.
     * The implementation provided below does this with almost millisecond accurately timed events.
     * However, these doubles below aren't as nice because they have a precision issue once times get long enough.
     * However, for me souch long timespans are not a concern
     */
    class TimeBarrier
    {
        private Stopwatch sw;
        private bool started;
        private double waitInterval;
        private double timerInterval;

        private double lastTimeStamp;

        public TimeBarrier()
        {
            waitInterval = 1.0;
            started = false;
            sw = new Stopwatch();
            timerInterval = 1.0 / Stopwatch.Frequency;
        }

        public void SetInterval(double interval)
        {
            waitInterval = interval;
        }

        public void Wait()
        {
            if (!started)
                return;
            double totalElapsed = sw.ElapsedTicks * timerInterval;
            double desiredTimeStamp = lastTimeStamp + waitInterval;
            double timeToWait = desiredTimeStamp - totalElapsed;
            if (timeToWait < 0.0)
                timeToWait = 0.0;
            int millisToWait = (int)(timeToWait * 1000.0);
            System.Threading.Thread.Sleep(millisToWait);
            lastTimeStamp = desiredTimeStamp;
        }

        public void Start()
        {
            if (started)
                return;
            started = true;
            lastTimeStamp = 0.0;
            sw.Restart();
        }

        public void Stop()
        {
            if (!started)
                return;
            started = false;
            sw.Stop();
        }
    }
}
