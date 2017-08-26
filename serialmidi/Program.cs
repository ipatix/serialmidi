using System;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;

namespace serialmidi
{
    class Program
    {
        static void Usage()
        {
            Console.Error.WriteLine("Usage: serialmidi <serial port> <song.mid> [<patchfile.bin>]");
            Console.Error.WriteLine("The serial port name will be something like COM1 on Windows and /dev/ttyS1 on Linux");
            Environment.Exit(1);
        }
        static void Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 3)
                Usage();
            string serialPortName = args[0];
            string midiFileName = args[1];
            string patchFileName;
            if (args.Length == 3)
                patchFileName = args[2];
            else
                patchFileName = null;

            SerialPort uart = new SerialPort(serialPortName);
            uart.BaudRate = 115200;
            uart.Parity = Parity.None;
            uart.DataBits = 8;
            uart.StopBits = StopBits.One;
            uart.Handshake = Handshake.None;

            uart.ReadTimeout = 500;
            uart.WriteTimeout = 500;

            uart.Open();

            // now write the patch file if one is supplied

            if (patchFileName != null)
            {
                byte[] patchBytes = File.ReadAllBytes(patchFileName);
                if (patchBytes.Length != 0x1800)
                    throw new Exception("Invalid patch file size (not 0x1800)");

                byte[] patchStart = { 0xF0 };
                byte[] patchEnd = { 0xF7 };
                uart.Write(patchBytes, 0, 1);
                uart.Write(patchBytes, 0, patchBytes.Length);
                uart.Write(patchBytes, 0, 1);
            }

            // now read midi and send it to device

            csmidi.MidiFile midi = new csmidi.MidiFile();
            midi.loadMidiFromFile(midiFileName);

            int[] trackEventIndex = new int[midi.midiTracks.Count];
            
            TimeBarrier tb = new TimeBarrier();
            tb.SetInterval(60.0 / (midi.timeDivision * 120.0)); // default midi speed 120 bpm
            tb.Start();

            Console.WriteLine("Playing MIDI...");

            long currentTick = 0;
            while (true)
            {
                Console.Write("tick: " + currentTick + " | ");
                int tracksStopped = 0;
                for (int i = 0; i < midi.midiTracks.Count; i++)
                {
                    Console.Write(" " + trackEventIndex[i] + "/" + midi.midiTracks[i].midiEvents.Count);
                }
                Console.WriteLine();
                for (int cTrk = 0; cTrk < midi.midiTracks.Count; cTrk++)
                {
                    csmidi.MidiTrack tr = midi.midiTracks[cTrk];
                    if (trackEventIndex[cTrk] >= tr.midiEvents.Count)
                    {
                        //Console.WriteLine("skipping track: " + cTrk);
                        tracksStopped++;
                        continue;
                    }

                    //Console.WriteLine("processing track: " + cTrk);
                    
                    while (currentTick >= tr.midiEvents[trackEventIndex[cTrk]].absoluteTicks)
                    {
                        //Console.WriteLine("crawling track: " + cTrk);
                        csmidi.MidiEvent ev = tr.midiEvents[trackEventIndex[cTrk]];
                        if (ev is csmidi.MessageMidiEvent)
                        {
                            csmidi.MessageMidiEvent mev = ev as csmidi.MessageMidiEvent;
                            byte[] data = mev.getEventData();
                            
                            uart.Write(data, 0, data.Length);
                            printHex(currentTick, data);
                        }
                        else if (ev is csmidi.MetaMidiEvent)
                        {
                            csmidi.MetaMidiEvent mev = ev as csmidi.MetaMidiEvent;
                            if (mev.getMetaType() == csmidi.MetaType.TempoSetting)
                            {
                                byte[] tempo = mev.getEventData();
                                Debug.Assert(tempo.Length == 6);
                                int us_per_beat = (tempo[3] << 16) | (tempo[4] << 8) | tempo[5];
                                double tickLength = (us_per_beat / 1000000.0) / midi.timeDivision;
                                Console.WriteLine("Set to {0} BPM", 1000000.0 / us_per_beat * 60.0);
                                tb.SetInterval(tickLength);
                            }
                        }
                        if (++trackEventIndex[cTrk] >= tr.midiEvents.Count)
                        {
                            tracksStopped++;
                            break;
                        }
                    }
                }
                //Console.WriteLine("stopped: " + tracksStopped);
                if (tracksStopped == midi.midiTracks.Count)
                    break;
                currentTick++;
                
                tb.Wait();
            }

            tb.Stop();
            uart.Close();
        }

        static void printHex(long time, byte[] data)
        {
            Console.Write(time + ": ");
            for (int i = 0; i < data.Length; i++)
            {
                Console.Write(data[i].ToString("X2") + " ");
            }
            Console.WriteLine();
        }
    }
}
