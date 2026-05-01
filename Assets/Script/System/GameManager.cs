using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Analytics;

public enum QType { Null }
public enum EType { Null }
public class GameManager : MonoBehaviour
{
    [Header("#GameControl")]
    public static GameManager instance;
    public bool isLive;
    public bool cantMove = false;
    [Header("#Status")]
    public float hp = 200;
    public float maxhp = 200;
    public float mana = 100;
    public float maxMana = 100;
    public float level = 1;
    public float exp = 0;
    public float maxExp = 10;
    public int combo = 0;
    [Header("#Stat")]
    public int str; //공
    public int vit; //체력, 방어력
    public int intel; //지능
    public int fth; //신앙
    [Header("#Atk")]
    public float attack; //공격력
    public float attackSpeed;
    [Header("#DEF")]
    public float def = 0;
    [Header("#AtkSpd")]
    public int atkSpdLV = 0;
    [Header("#CRIT")]
    public float crit = 5;
    [Header("#CRITDMG")]
    public float crDmg = 50;
    [Header("#QSkill")]
    public QType qType;
    public int qDmgLV = 1;
    public float qAtk = 1.0f; //Q 공격력 계수
    public float qCooltime; //Q 쿨타임
    public float qElapsedTime = 0; //Q 경과 쿨타임
    public bool isQCool = false; //Q 쿨인지
    [Header("#ESkill")]
    public EType eType;
    public int eDmgLV = 1;
    public float eAtk = 1.0f;
    public float eCooltime;
    public float eElapsedTime = 0;
    public bool isECool = false;
    void Awake() {
        if(GameManager.instance == null) {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else {
            Destroy (gameObject);
        }
    }
    // Update is called once per frame
    void Update()
    {
        if(isLive == false) {
            return;
        }
        HpMPChange();
    }
    public void HpMPChange() { //HP 변화
        //체력이 최대치 안넘게
        if(hp > maxhp) {
            hp = maxhp;
        }
        //Mana 최대치 안넘게
        if(mana > maxMana) {
            mana = maxMana;
        }
        if(exp >= maxExp) {
            exp = 0;
            level++;
            maxExp = level * 10; 
        }
        if(crit > 100) {
            crit = 100;
        }
    }
    public void Stop()
    {
        isLive = false; //업데이트 함수 쓰는건 다 걸어주기
        Time.timeScale = 0; //정지
    }
    public void Resume()
    {
        isLive = true;
        Time.timeScale = 1; //1배속
    }
    public IEnumerator QCasting() //Q 쿨타임
    {
        qElapsedTime = 0;
        while (qElapsedTime < qCooltime)
        {
            qElapsedTime += Time.deltaTime;
            yield return null;
        }
        isQCool = false;
    }
    public IEnumerator ECasting() //E 쿨타임
    {
        eElapsedTime = 0;
        while (eElapsedTime < eCooltime)
        {
            eElapsedTime += Time.deltaTime;
            yield return null;
        }
        isECool = false;
    }
}
