﻿using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using UnityEngine;

public class UdpRecordPlayer : MonoBehaviour
{
    public UdpSender sender;
    Queue<byte[]> recordQueue = new Queue<byte[]>();
    public string recordFilePath = "udpRec.data";
    public string playFilePath = "recordedUdp/recorded.udp";

    Thread recorder;
    Thread player;
    float startTime;
    public float time;

    bool recording;
    bool playing;
    public string exeption;

    private void OnDestroy()
    {
        Stop();
    }

    public void EnqueueData(byte[] data)
    {
        if (!recording)
            return;
        recordQueue.Enqueue(data);
        time = Time.time - startTime;
    }

    [ContextMenu("Start Recording")]
    public void StartRecording()
    {
        recording = true;
        startTime = Time.time;
        recordQueue.Clear();

        if (recorder != null)
            recorder.Abort();
        recorder = new Thread(RecordLoop);
        writeCount = 0;
        recorder.Start();
    }
    [ContextMenu("Play RecordedData")]
    public void Play()
    {
        playing = true;
        startTime = Time.time;
        time = 0;

        if (player != null)
            player.Abort();
        player = new Thread(PlayLoop);
        player.Start();
    }
    [ContextMenu("Stop Rec-Play")]
    public void Stop()
    {
        recording = false;
        playing = false;
        if (recorder != null)
            recorder.Abort();
        recorder = null;
        if (player != null)
            player.Abort();
        player = null;
    }

    private void Update()
    {
        if (playing)
            time = Time.time - startTime;
    }

    void RecordLoop()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            while (recording)
                lock (recordQueue)
                    while (0 < recordQueue.Count)
                    {
                        var data = recordQueue.Dequeue();
                        try
                        {
                            if (data != null)
                            {
                                writer.Write(time);
                                writer.Write(data.Length);
                                writer.Write(data);
                            }
                        }
                        catch (System.Exception e)
                        {
                            exeption = e.ToString();
                        }
                    }
            stream.Flush();
            File.WriteAllBytes(recordFilePath, stream.GetBuffer());

            writer.Close();
            stream.Close();
        }
    }

    void PlayLoop()
    {
        var recordedData = File.ReadAllBytes(playFilePath);
        using (var stream = new MemoryStream(recordedData))
        using (var reader = new BinaryReader(stream))
        {
            while (playing)
            {
                try
                {
                    var nextTime = reader.ReadSingle();
                    var count = reader.ReadInt32();
                    var data = reader.ReadBytes(count);
                    while (time < nextTime && playing)
                    {
                        //Playを止めたときとか、このループを出れるように作っておかないと死ぬ
                    }
                    sender.Send(data);
                }
                catch (EndOfStreamException)
                {
                    playing = false;
                    reader.Close();
                    stream.Close();
                    return;
                }
            }
            reader.Close();
            stream.Close();
        }
    }
}