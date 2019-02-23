﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.csutil;
using UnityEngine.UI;
using System;
using com.csutil.ui;

namespace com.csutil.tests.ui {

    public class ExampleUi2_MainScreen : MonoBehaviour {

        public ExampleUi1.MyUserModel currentUser = new ExampleUi1.MyUserModel() { userName = "Carl" };

        void Start() {
            var links = gameObject.GetLinkMap();
            links.Get<Button>("OptionsButton").SetOnClickAction(delegate {
                ScreenStack.SwitchToScreen(gameObject, "ExampleUi2_OptionsScreen");
            });
            links.Get<Button>("UserDetailsButton").SetOnClickAction(ShowUserUi);
        }

        private void ShowUserUi(GameObject buttonGo) {
            GameObject ui = ScreenStack.SwitchToScreen(gameObject, "MyUserUi1");

            ExampleUi1.MyUserUi presenter = new ExampleUi1.MyUserUi();
            presenter.LoadModelIntoView(currentUser, ui);

            var links = ui.GetLinkMap();
            links.Get<Button>("Save").SetOnClickAction(delegate { ScreenStack.SwitchBackToLastScreen(ui); });
            AssertV2.AreEqual(currentUser.userName, links.Get<InputField>("Name").text, "userName");
        }
    }

}