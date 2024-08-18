using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using UnityEngine.UI;

public class emotion : MonoBehaviour
{
    public AudioSource beatSource;
    public AudioSource leadSource;
    public AudioSource melodySource;
    public Text emotionText;
    public Animator animator;
    private TcpClient client;
    private NetworkStream stream;

    [Header("Beats")]
    public List<AudioClip> latinBeats = new List<AudioClip>();
    public List<AudioClip> raveBeats = new List<AudioClip>();
    public List<AudioClip> ninetiesBeats = new List<AudioClip>();

    [Header("Leads")]
    public List<AudioClip> latinLeads = new List<AudioClip>();
    public List<AudioClip> raveLeads = new List<AudioClip>();
    public List<AudioClip> ninetiesLeads = new List<AudioClip>();

    [Header("Melodies")]
    public List<AudioClip> latinMelodies = new List<AudioClip>();
    public List<AudioClip> raveMelodies = new List<AudioClip>();
    public List<AudioClip> ninetiesMelodies = new List<AudioClip>();

    [Header("Pitch Transition Settings")]
    public float pitchDownDuration = 1f; // Duration for pitch to decrease
    public float pitchUpDuration = 1f;   // Duration for pitch to increase
    public float songDuration = 20f;   // Duration of each song loop

    [Header("Happiness Detection Settings")]
    public float happinessThreshold = 50f; // Percentage threshold to consider happiness

    private Dictionary<AudioClip, float> beatHappinessScores = new Dictionary<AudioClip, float>();
    private Dictionary<AudioClip, float> leadHappinessScores = new Dictionary<AudioClip, float>();

    private AudioClip bestBeat;
    private AudioClip happiestLead;
    private float highestHappiness = 0f;
    private bool bestBeatIsLatin = false;
    private bool bestBeatIsRave = false;
    private bool bestBeatIsNineties = false;

    // Declaring the emotion variables here
    private string currentEmotion = "neutral";
    private float currentEmotionScore = 0f;

    void Start()
    {
        ConnectToPython();
        StartCoroutine(ReceiveEmotionData());
    }

    public void play()
    {
        StartPlayingAllCategories(); // Begin sequential playback
    }

    public void skip()
    {
        leadSource.Stop();
        StartPlayingAllCategories(); // Begin sequential playback
    }

    public void pause()
    { 
        leadSource.Pause();
        beatSource.Pause();
        melodySource.Pause();
        animator.speed = 0f;
    }

    public void unpause()
    {
        leadSource.UnPause();
        beatSource.UnPause();
        melodySource.UnPause();
        animator.speed = 1f;
    }

    void ConnectToPython()
    {
        try
        {
            client = new TcpClient("localhost", 8080);
            stream = client.GetStream();
        }
        catch (SocketException e)
        {
            Debug.LogError("Socket error: " + e.Message);
        }
    }

    IEnumerator ReceiveEmotionData()
    {
        byte[] buffer = new byte[1024];
        while (true)
        {
            if (stream != null && stream.DataAvailable)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string[] parts = response.Split(':');
                if (parts.Length == 2)
                {
                    string emotion = parts[0].Trim();
                    float score = float.Parse(parts[1].Trim());

                    // Update the detected emotion and its score
                    currentEmotion = emotion;  // Assign to the global variable
                    currentEmotionScore = score;

                    emotionText.text = $"{emotion}: {score}%";
                }
            }

            yield return new WaitForSeconds(0.04f);
        }
    }

    public void PlayAll(List<AudioClip> clips, AudioSource audioSource, Dictionary<AudioClip, float> happinessScores)
    {
        StartCoroutine(PlayCategoryWithPitch(clips, audioSource, happinessScores));
    }

    IEnumerator PlayCategoryWithPitch(List<AudioClip> clips, AudioSource audioSource, Dictionary<AudioClip, float> happinessScores)
    {
        foreach (var clip in clips)
        {
            List<float> happinessScoresForClip = new List<float>();
            int totalEmotionChecks = 0;
            float elapsedTime = 0f;

            audioSource.clip = clip;
            audioSource.loop = true;
            audioSource.pitch = 1f;
            audioSource.Play();

            while (elapsedTime < songDuration)
            {
                totalEmotionChecks++;
                elapsedTime += Time.deltaTime;

                if (currentEmotion.ToLower() == "happy")
                {
                    happinessScoresForClip.Add(currentEmotionScore);
                }

                if (elapsedTime >= songDuration - pitchDownDuration)
                {
                    float t = (elapsedTime - (songDuration - pitchDownDuration)) / pitchDownDuration;
                    audioSource.pitch = Mathf.Lerp(1f, 0.05f, t);
                }

                yield return null;
            }

            float happinessPercentage = (float)happinessScoresForClip.Count / totalEmotionChecks * 100f;

            if (happinessPercentage >= happinessThreshold && happinessScoresForClip.Count > 0)
            {
                float totalHappiness = 0f;
                foreach (float score in happinessScoresForClip)
                {
                    totalHappiness += score;
                }
                float averageHappiness = totalHappiness / happinessScoresForClip.Count;

                happinessScores[clip] = averageHappiness;
                Debug.Log($"Clip: {clip.name} finished. Average Happiness: {averageHappiness}%, Happiness Detected for {happinessPercentage}% of the time.");
            }
            else
            {
                Debug.Log($"Clip: {clip.name} finished. No Happiness Detected. Happiness was only detected {happinessPercentage}% of the time.");
            }

            audioSource.Stop();
            audioSource.pitch = 1f;
        }
    }

    IEnumerator PlayAllCategoriesSequentially()
    {
        yield return StartCoroutine(PlayCategoryWithPitch(latinBeats, beatSource, beatHappinessScores));
        yield return StartCoroutine(PlayCategoryWithPitch(raveBeats, beatSource, beatHappinessScores));
        yield return StartCoroutine(PlayCategoryWithPitch(ninetiesBeats, beatSource, beatHappinessScores));

        FindAndLoopBestBeatAndLead();
    }

    void FindAndLoopBestBeatAndLead()
    {
        bestBeat = null;
        happiestLead = null;
        highestHappiness = 0f;

        // Find the best beat
        foreach (var entry in beatHappinessScores)
        {
            if (entry.Value > highestHappiness)
            {
                highestHappiness = entry.Value;
                bestBeat = entry.Key;
            }
        }

        if (bestBeat != null)
        {
            Debug.Log($"The beat with the highest average happiness is: {bestBeat.name} with an average happiness score of {highestHappiness}%.");

            beatSource.clip = bestBeat;
            beatSource.loop = true;
            beatSource.Play();

            // Determine the category of the best beat
            bestBeatIsLatin = latinBeats.Contains(bestBeat);
            bestBeatIsRave = raveBeats.Contains(bestBeat);
            bestBeatIsNineties = ninetiesBeats.Contains(bestBeat);

            // Play and find the happiest lead in the same category
            if (bestBeatIsLatin)
            {
                StartCoroutine(PlayLeadsWithBestBeat(latinLeads));
            }
            else if (bestBeatIsRave)
            {
                StartCoroutine(PlayLeadsWithBestBeat(raveLeads));
            }
            else if (bestBeatIsNineties)
            {
                StartCoroutine(PlayLeadsWithBestBeat(ninetiesLeads));
            }
        }
        else
        {
            Debug.Log("No beat detected as the happiest.");
        }
    }

    IEnumerator PlayLeadsWithBestBeat(List<AudioClip> leadsInCategory)
    {
        happiestLead = null;
        float highestLeadHappiness = 0f;

        foreach (var lead in leadsInCategory)
        {
            yield return StartCoroutine(PlayLeadAndEvaluateHappiness(lead));

            if (leadHappinessScores.ContainsKey(lead) && leadHappinessScores[lead] > highestLeadHappiness)
            {
                highestLeadHappiness = leadHappinessScores[lead];
                happiestLead = lead;
            }
        }

        if (happiestLead != null)
        {
            Debug.Log($"The happiest lead is: {happiestLead.name} with an average happiness score of {highestLeadHappiness}%.");
            leadSource.clip = happiestLead;
            leadSource.loop = true;
            leadSource.Play();
        }
        else
        {
            Debug.Log("No lead detected as the happiest.");
        }
    }

    IEnumerator PlayLeadAndEvaluateHappiness(AudioClip lead)
    {
        List<float> happinessScoresForClip = new List<float>();
        int totalEmotionChecks = 0;
        float elapsedTime = 0f;

        leadSource.clip = lead;
        leadSource.loop = true;
        leadSource.pitch = 1f;
        leadSource.Play();

        while (elapsedTime < songDuration)
        {
            totalEmotionChecks++;
            elapsedTime += Time.deltaTime;

            if (currentEmotion.ToLower() == "happy")
            {
                happinessScoresForClip.Add(currentEmotionScore);
            }

            if (elapsedTime >= songDuration - pitchDownDuration)
            {
                float t = (elapsedTime - (songDuration - pitchDownDuration)) / pitchDownDuration;
                leadSource.pitch = Mathf.Lerp(1f, 0.05f, t);
            }

            yield return null;
        }

        float happinessPercentage = (float)happinessScoresForClip.Count / totalEmotionChecks * 100f;

        if (happinessPercentage >= happinessThreshold && happinessScoresForClip.Count > 0)
        {
            float totalHappiness = 0f;
            foreach (float score in happinessScoresForClip)
            {
                totalHappiness += score;
            }
            float averageHappiness = totalHappiness / happinessScoresForClip.Count;

            leadHappinessScores[lead] = averageHappiness;
            Debug.Log($"Lead: {lead.name} finished. Average Happiness: {averageHappiness}%, Happiness Detected for {happinessPercentage}% of the time.");
        }
        else
        {
            Debug.Log($"Lead: {lead.name} finished. No Happiness Detected. Happiness was only detected {happinessPercentage}% of the time.");
        }

        leadSource.Stop();
        leadSource.pitch = 1f;
    }

    public void StartPlayingAllCategories()
    {
        StartCoroutine(PlayAllCategoriesSequentially());
    }
}
