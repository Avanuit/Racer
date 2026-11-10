using System;
using System.Collections;
using UnityEngine;

/// Cuenta vueltas con checkpoints en orden. checkpoints[0] = línea de salida/meta.
/// Incluye cuenta regresiva y respawn (tecla R) en el último checkpoint.
public class LapManager : MonoBehaviour
{
    public Checkpoint[] checkpoints;
    public Rigidbody car;
    public PrometeoCarController pcc;
    public int totalLaps = 3;
    public int countdownSeconds = 3;
    public KeyCode respawnKey = KeyCode.R;

    public int CurrentLap { get; private set; } = 1;
    public float LapTime { get; private set; }
    public float BestLap { get; private set; } = float.PositiveInfinity;
    public float LastLap { get; private set; }
    public bool Running { get; private set; }
    public bool Finished { get; private set; }
    public string CountdownLabel { get; private set; } = "";

    public event Action<int, float> OnLapCompleted;
    public event Action OnRaceFinished;

    int next = 1;
    Transform lastSpawn;

    void Awake()
    {
        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpoints[i].manager = this;
            checkpoints[i].index = i;
        }
        lastSpawn = checkpoints[0].transform;
    }

    IEnumerator Start()
    {
        if (pcc) pcc.enabled = false;
        for (int s = countdownSeconds; s > 0; s--)
        {
            CountdownLabel = s.ToString();
            yield return new WaitForSeconds(1f);
        }
        CountdownLabel = "¡YA!";
        if (pcc) pcc.enabled = true;
        Running = true;
        yield return new WaitForSeconds(1f);
        CountdownLabel = "";
    }

    void Update()
    {
        if (Running && !Finished) LapTime += Time.deltaTime;
        if (Input.GetKeyDown(respawnKey)) Respawn();
    }

    public void Pass(int index)
    {
        if (!Running || Finished || index != next) return;

        if (index == 0)
        {
            LastLap = LapTime;
            BestLap = Mathf.Min(BestLap, LapTime);
            OnLapCompleted?.Invoke(CurrentLap, LapTime);
            if (CurrentLap >= totalLaps)
            {
                Finished = true;
                CountdownLabel = "META";
                OnRaceFinished?.Invoke();
                return;
            }
            CurrentLap++;
            LapTime = 0f;
            next = 1;
        }
        else
        {
            lastSpawn = checkpoints[index].transform;
            next = (index + 1) % checkpoints.Length;
        }
    }

    public void Respawn()
    {
        car.transform.SetPositionAndRotation(lastSpawn.position + Vector3.up * 1f,
            Quaternion.Euler(0f, lastSpawn.eulerAngles.y, 0f));
        CarUtil.SetVel(car, Vector3.zero);
        car.angularVelocity = Vector3.zero;
    }
}
