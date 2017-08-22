﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Distractors;

namespace AXCPT {
	public enum TrialItem {A, B, X, Y};

	// Class to hold the cue and target types
	public class TrialType {
		public TrialItem cue { get; set; }
		public TrialItem target { get; set; }

		public TrialType(TrialItem cue, TrialItem target) {
			this.cue = cue;
			this.target = target;
		}

		public bool CheckResponse (string buttonPressed) {
			if (cue == TrialItem.A && target == TrialItem.X) {
				if (buttonPressed == "Button1") {
					return true;
				} else {
					return false;
				}
			}
			if (buttonPressed == "Button2") {
				return true;
			} else {
				return false;
			}
		}

		public override string ToString() {
			return cue.ToString () + target.ToString ();
		}
	}

	public enum TrialState { Starting, Instruction1, Instruction2, Instruction3, Instruction4, Instruction5, Ready, Cue, ISI, Target, ITI, Correct, Incorrect, Slow, Ending };

	public static class TrialStateExtensions {
		public static float Duration(this TrialState state) {
			switch (state) {
			case TrialState.Cue:    return  1.0f;
			case TrialState.ISI:    return  2.0f;
			case TrialState.Target: return  0.5f;
			case TrialState.ITI:    return  1.2f;
			default:                return -1.0f;
			}
		}

		public static string Instructions(this TrialState state, Textures textures) {
			switch (state) {
			case TrialState.Starting:     return "<size=40>AX-CPT Task</size>\n\n<size=30><i>Press any key to continue.</i></size>";
			case TrialState.Instruction1: return "<size=60>For each trial,\nyou will see\na pair of\npictures with\ntext below.\n\n</size><size=30><i>Press any key to continue.</i></size>";
			case TrialState.Instruction2: return "<size=60>The 1st picture will\nhave <color=blue>blue</color> text.\nThe 2nd picture will\nhave <color=orange>orange</color> text.\nYour goal is to\nfind the special pair.</size>\n\n<size=30><i>Press any key to continue.</i></size>";
			case TrialState.Instruction3: return "<size=60>The special pair\nis <color=blue>" + textures.a_group [0].name + "</color>\nbefore <color=orange>" + textures.x_group [0].name + "</color>.\nIf you see\nthis pair, press <b>1</b>.</size>\n\n<size=30><i>Press any key to continue.</i></size>";
			case TrialState.Instruction4: return "<size=60>For all other\n pairs, press <b>2</b>.</size>\n\n<size=30><i>Press any key to continue.</i></size>";
			case TrialState.Instruction5: return "<size=60>You must respond\nbefore the next\npair appears.</size>\n\n<size=30><i>Press any key to continue.</i></size>";
			case TrialState.Ready:        return "<size=60><b>Remember: <color=blue>" + textures.a_group [0].name + "</color>\nbefore <color=orange>" + textures.x_group [0].name + "</color>.</b></size>\n\n<size=30><i>Press 1 to begin task.</i></size>";
			default:                      return "";
			}
		}

		public static TrialState Next(this TrialState state) {
			switch (state) {
			case TrialState.Starting:     return TrialState.Instruction1;
			case TrialState.Instruction1: return TrialState.Instruction2;
			case TrialState.Instruction2: return TrialState.Instruction3;
			case TrialState.Instruction3: return TrialState.Instruction4;
			case TrialState.Instruction4: return TrialState.Instruction5;
			case TrialState.Instruction5: return TrialState.Ready;
			case TrialState.Ready:        return TrialState.Cue;
			case TrialState.Cue:          return TrialState.ISI;
			case TrialState.ISI:          return TrialState.Target;
			case TrialState.Target:       return TrialState.ITI;
			case TrialState.ITI:          return TrialState.Cue;
			case TrialState.Slow:         return TrialState.Cue;
			default:                      return TrialState.Ending;
			}
		}

		public static Texture GetTexture(this TrialState state, TrialType type, Textures textures) {
			switch (state) {
			case TrialState.Cue:    return textures.Get(type.cue);
			case TrialState.ISI:    return textures.isi;
			case TrialState.Target: return textures.Get(type.target);
			case TrialState.ITI:    return textures.iti;
			default:                return null;
			}
		}
	}

	public class TrialOutput {
		public readonly int num;
		public readonly TrialType type;
		public readonly RecordResponses.Response response;

		public TrialOutput(int num, TrialType type, RecordResponses.Response response) {
			this.num = num;
			this.type = type;
			this.response = response;
		}

		public override string ToString() {
			return num.ToString () + "," +
			type.ToString () + "," +
			response.buttonPressed + "," +
			(response == RecordResponses.EMPTY_RESPONSE ? "" : response.responseTime.ToString ());
		}
	}

	public class AXCPT : MonoBehaviour {
		public ShowText whiteboardText;
		public ShowImage whiteboardImage;
		public TrialList trials;
		public Textures textures;
		public RecordResponses recorder;
		public DistractorController distractorController;

		private int currentTrial;
		private TrialState trialState;
		private CountdownTimer timer;
		private CountdownTimer recordingTimer; 
		private CSVWriter recordResults;

		void Start () {
			currentTrial = 0;
			trialState = TrialState.Starting;
			timer = new CountdownTimer (-1);
			recordingTimer = new CountdownTimer (1.5f);
			recordResults = new CSVWriter ("results.csv");
			recordResults.WriteRow ("trial_number,trial_type,button_pressed,reaction_time");
			print ("Starting AX-CPT");
            whiteboardText = GameObject.Find("WhiteBoardWithDisplay").GetComponent<ShowText>();
            whiteboardImage = GameObject.Find("WhiteBoardWithDisplay").GetComponent<ShowImage>();
            whiteboardText.SetText (trialState.Instructions(textures));
			whiteboardText.Show ();
		}

		void Update () {
			var finishInstructions = trialState == TrialState.Ready && Input.GetButtonDown ("Button1");
			var finishState = (int)trialState >= (int)TrialState.Cue && (int)trialState <= (int)TrialState.ITI && timer.isComplete;

			if (((int)trialState < (int)TrialState.Ready) && Input.anyKeyDown) {
				trialState = trialState.Next ();
				whiteboardText.SetText (trialState.Instructions(textures));
				return;
			} 
			if (finishInstructions || finishState) {
				whiteboardText.Hide ();
				if (finishInstructions) {
					distractorController.gameObject.SetActive (true);
				}
				if (trialState == TrialState.ITI) {
					currentTrial++;
					if (currentTrial == trials.trialTypes.Length) {
						trialState = TrialState.Ending;
						recordResults.Close ();
						whiteboardImage.Hide ();
						distractorController.gameObject.SetActive (false);
						return;
					}
				}

				trialState = trialState.Next ();
				print ("Starting state " + trialState);

				whiteboardImage.SetTexture(trialState.GetTexture(trials.trialTypes[currentTrial], textures));
				whiteboardImage.Show ();

				timer.duration = trialState.Duration ();
				if (trialState == TrialState.Target) {
					recorder.StartRecording ();
					recordingTimer.Start ();
				}
				timer.Start ();
			}
			if (recorder.isRecording && recordingTimer.isComplete) {
				var response = recorder.StopRecording ();
				var output = new TrialOutput (currentTrial, trials.trialTypes [currentTrial], response);
				recordResults.WriteRow (output.ToString());
			}
		}
	}
}
