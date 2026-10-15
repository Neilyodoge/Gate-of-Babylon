param([string]$OutputName='TataFarmAdventure.exe')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$farmBuildDir=Join-Path $projectRoot 'src'
$assetDir=Join-Path $projectRoot 'assets'
$outputDir=Join-Path $projectRoot 'artifacts/build'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
if([IO.Path]::GetFileName($OutputName) -ne $OutputName){throw 'OutputName must be a filename.'}
$netDir='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$compilerArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+',"/out:$outputDir\$OutputName","/reference:$netDir\WPF\PresentationFramework.dll","/reference:$netDir\WPF\PresentationCore.dll","/reference:$netDir\WPF\WindowsBase.dll",'/reference:System.Xaml.dll','/reference:System.Xml.dll','/main:FarmProgram')
foreach($form in @(@('frames','BabyFrame'),@('frames-evolved','AdultFrame'),@('frames-enemy0','Friend0'),@('frames-enemy1','Friend1'),@('frames-enemy2','Friend2'),@('frames-seed1','Seed1Frame'),@('frames-seed2','Seed2Frame'),@('frames-seed3','Seed3Frame'),@('frames-seed4','Seed4Frame'))){
    $index=0
    foreach($file in (Get-ChildItem -LiteralPath (Join-Path $assetDir $form[0]) -Filter 'frame-*.png'|Sort-Object Name)){
        $compilerArgs+='/resource:'+ $file.FullName +','+$form[1]+$index.ToString('000')+'.png';$index++
    }
}
$compilerArgs+="$farmBuildDir\GameModel.cs";$compilerArgs+="$farmBuildDir\FarmModel.cs";$compilerArgs+="$farmBuildDir\FarmApp.cs";$compilerArgs+="$farmBuildDir\FarmPanel.cs";$compilerArgs+="$farmBuildDir\FarmUX.cs"
$compilerArgs+="$farmBuildDir\FarmDesktop.cs";$compilerArgs+='/reference:System.Windows.Forms.dll';$compilerArgs+='/reference:System.Drawing.dll'
$compilerArgs+="$farmBuildDir\DesktopLayout.cs";$compilerArgs+="$farmBuildDir\FarmHeadlessTests.cs"
$compilerArgs+="$farmBuildDir\FarmUXReviewTests.cs"
$compilerArgs+="$farmBuildDir\FarmPetHover.cs";$compilerArgs+="$farmBuildDir\FarmPetStartTests.cs"
$compilerArgs+="$farmBuildDir\FarmPicnicCompanion.cs";$compilerArgs+="$farmBuildDir\FarmPicnicCompanionTests.cs"
$compilerArgs+="$farmBuildDir\FarmChapterGuide.cs";$compilerArgs+="$farmBuildDir\FarmChapterGuideTests.cs"
$compilerArgs+="$farmBuildDir\FarmChapterReturn.cs";$compilerArgs+="$farmBuildDir\FarmChapterReturnTests.cs"
$compilerArgs+="$farmBuildDir\FarmCompanionFocusTests.cs"
$compilerArgs+="$farmBuildDir\FarmSkillCare.cs";$compilerArgs+="$farmBuildDir\FarmSkillCareTests.cs"
$compilerArgs+="$farmBuildDir\FarmJourneyHealth.cs";$compilerArgs+="$farmBuildDir\FarmJourneyHealthTests.cs"
$compilerArgs+="$farmBuildDir\FarmCrystalJourney.cs";$compilerArgs+="$farmBuildDir\FarmCrystalJourneyTests.cs"
$compilerArgs+="$farmBuildDir\FarmCropRecovery.cs";$compilerArgs+="$farmBuildDir\FarmCropRecoveryTests.cs"
$compilerArgs+="$farmBuildDir\FarmJourney.cs";$compilerArgs+="$farmBuildDir\FarmJourneyPanel.cs";$compilerArgs+="$farmBuildDir\FarmDecorations.cs";$compilerArgs+="$farmBuildDir\JourneyTests.cs"
$compilerArgs+="$farmBuildDir\FarmPets.cs";$compilerArgs+="$farmBuildDir\FarmPetPanel.cs";$compilerArgs+="$farmBuildDir\PetTests.cs"
$compilerArgs+="$farmBuildDir\FarmProgression.cs";$compilerArgs+="$farmBuildDir\FarmHandbook.cs"
$compilerArgs+="$farmBuildDir\ProgressionTests.cs"
$compilerArgs+="$farmBuildDir\FarmTheme.cs";$compilerArgs+="$farmBuildDir\FarmUI.cs"
$compilerArgs+="$farmBuildDir\FarmUITests.cs"
$compilerArgs+="$farmBuildDir\FarmTravelUI.cs"
$compilerArgs+="$farmBuildDir\FarmJournalUI.cs"
$compilerArgs+="$farmBuildDir\FarmTravelUITests.cs"
$compilerArgs+="$farmBuildDir\FarmPresence.cs"
$compilerArgs+="$farmBuildDir\FarmPresenceTests.cs"
$compilerArgs+="$farmBuildDir\FarmManagement.cs";$compilerArgs+="$farmBuildDir\FarmWorkshopUI.cs"
$compilerArgs+="$farmBuildDir\FarmManagementTests.cs"
$compilerArgs+="$farmBuildDir\FarmDecorating.cs";$compilerArgs+="$farmBuildDir\FarmDecoratingUI.cs"
$compilerArgs+="$farmBuildDir\FarmDecoratingTests.cs"
$compilerArgs+="$farmBuildDir\FarmCare.cs";$compilerArgs+="$farmBuildDir\FarmCareUI.cs";$compilerArgs+="$farmBuildDir\FarmCareTests.cs"
$compilerArgs+="$farmBuildDir\FarmBattleReview.cs";$compilerArgs+="$farmBuildDir\FarmBattleReviewUI.cs";$compilerArgs+="$farmBuildDir\FarmBattleReviewTests.cs"
$compilerArgs+="$farmBuildDir\FarmPractice.cs";$compilerArgs+="$farmBuildDir\FarmPracticeUI.cs";$compilerArgs+="$farmBuildDir\FarmPracticeTests.cs"
$compilerArgs+="$farmBuildDir\FarmInspiration.cs";$compilerArgs+="$farmBuildDir\FarmInspirationUI.cs";$compilerArgs+="$farmBuildDir\FarmInspirationTests.cs"
$compilerArgs+="$farmBuildDir\FarmPicnic.cs";$compilerArgs+="$farmBuildDir\FarmPicnicUI.cs";$compilerArgs+="$farmBuildDir\FarmPicnicTests.cs"
$compilerArgs+="$farmBuildDir\FarmHomecoming.cs";$compilerArgs+="$farmBuildDir\FarmHomecomingUI.cs";$compilerArgs+="$farmBuildDir\FarmHomecomingTests.cs"
$compilerArgs+="$farmBuildDir\FarmHomeUX.cs";$compilerArgs+="$farmBuildDir\FarmHomeUXTests.cs"
$compilerArgs+="$farmBuildDir\FarmFocusUX.cs";$compilerArgs+="$farmBuildDir\FarmFocusUXTests.cs"
$compilerArgs+="$farmBuildDir\FarmReturnUX.cs";$compilerArgs+="$farmBuildDir\FarmReturnUXTests.cs"
$compilerArgs+="$farmBuildDir\FarmTrialUX.cs";$compilerArgs+="$farmBuildDir\FarmTrialTests.cs"
$compilerArgs+="$farmBuildDir\FarmGuide.cs";$compilerArgs+="$farmBuildDir\FarmGuideTests.cs"
$compilerArgs+="$farmBuildDir\FarmWorldGuide.cs"
$compilerArgs+="$farmBuildDir\FarmGentleGuide.cs"
$compilerArgs+="$farmBuildDir\FarmHomeGuide.cs"
$compilerArgs+="$farmBuildDir\FarmQuietHome.cs"
$compilerArgs+="$farmBuildDir\FarmEveryday.cs"
$compilerArgs+="$farmBuildDir\FarmSmallJourney.cs"
$compilerArgs+="$farmBuildDir\FarmSmallJourneyScene.cs"
$compilerArgs+="$farmBuildDir\FarmCourtyardThings.cs"
$compilerArgs+="$farmBuildDir\FarmSmallGrowth.cs"
$compilerArgs+="$farmBuildDir\FarmSmallDestination.cs"
$compilerArgs+="$farmBuildDir\FarmLife.cs"
$compilerArgs+="$farmBuildDir\FarmLifeCareResult.cs"
$compilerArgs+="$farmBuildDir\FarmAwayHarvest.cs"
$compilerArgs+="$farmBuildDir\FarmContinuityTests.cs"
$compilerArgs+="$farmBuildDir\FarmTravelContinuity.cs"
$compilerArgs+="$farmBuildDir\FarmTravelContinuityTests.cs"
$compilerArgs+="$farmBuildDir\FarmPreparationCards.cs"
$compilerArgs+="$farmBuildDir\FarmPreparationTests.cs"
$compilerArgs+="$farmBuildDir\FarmSmallResources.cs"
$compilerArgs+="$farmBuildDir\FarmSmallResourceTests.cs"
$compilerArgs+="$farmBuildDir\FarmLifeTests.cs"
$compilerArgs+="$farmBuildDir\FarmLifeSupplies.cs"
$compilerArgs+="$farmBuildDir\FarmLifeCourtyard.cs"
$compilerArgs+="$farmBuildDir\FarmFirstSmallJourney.cs"
$compilerArgs+="$farmBuildDir\FarmSmallProgressionTests.cs"
$compilerArgs+="$farmBuildDir\FarmLaterLoopTests.cs"
$compilerArgs+="$farmBuildDir\FarmEverydayTests.cs"
$compilerArgs+="$farmBuildDir\FarmAccessibility.cs"
$compilerArgs+="$farmBuildDir\FarmKeyboard.cs"
$compilerArgs+="$farmBuildDir\FarmReadingTests.cs"
$compilerArgs+="$farmBuildDir\FarmKeyboardTests.cs"
$compilerArgs+="$farmBuildDir\FarmReadingUITests.cs"
$compilerArgs+="$farmBuildDir\FarmFinalUXTests.cs"
$compilerArgs+="$farmBuildDir\FarmGuideCards.cs"
$compilerArgs+="$farmBuildDir\FarmLessonScene.cs"
$compilerArgs+="$farmBuildDir\FarmPanelPresence.cs"
$compilerArgs+="$farmBuildDir\FarmSmallStepsTests.cs"
$compilerArgs+="$farmBuildDir\FarmFirstOuting.cs"
$compilerArgs+="$farmBuildDir\FarmFirstOutingTests.cs"
$compilerArgs+="$farmBuildDir\FarmFirstHomecoming.cs"
$compilerArgs+="$farmBuildDir\FarmFirstHomecomingTests.cs"
$compilerArgs+="$farmBuildDir\FarmCompanionFeedback.cs"
$compilerArgs+="$farmBuildDir\FarmGuideBerries.cs";$compilerArgs+="$farmBuildDir\FarmGuideBerriesTests.cs"
$compilerArgs+="$farmBuildDir\FarmFirstFeeding.cs";$compilerArgs+="$farmBuildDir\FarmFirstFeedingTests.cs"
$compilerArgs+="$farmBuildDir\FarmHarvestFeedback.cs"
$compilerArgs+="$farmBuildDir\FarmCompanionFeedbackTests.cs"
$compilerArgs+="$farmBuildDir\FarmJourneyScene.cs"
$compilerArgs+="$farmBuildDir\FarmQuietUXTests.cs"
$compilerArgs+="$farmBuildDir\FarmWorldGuideTests.cs"
foreach($icon in (Get-ChildItem -LiteralPath "$assetDir\pet-icons" -Filter "*.png")){$compilerArgs+="/resource:$($icon.FullName),PetIcon-$($icon.Name)"}
$compilerArgs+='/reference:System.Xml.Linq.dll'
$compilerArgs+="$farmBuildDir\FarmCards.cs";$compilerArgs+="$farmBuildDir\FarmCardsUI.cs";$compilerArgs+="$farmBuildDir\FarmCardsTests.cs"
$compilerArgs+="$farmBuildDir\FarmCardFeedback.cs";$compilerArgs+="$farmBuildDir\FarmCardFeedbackTests.cs";$compilerArgs+="$farmBuildDir\FarmCardBranches.cs";$compilerArgs+="$farmBuildDir\FarmCardBranchTests.cs"
$compilerArgs+="$farmBuildDir\FarmCardDamage.cs";$compilerArgs+="$farmBuildDir\FarmCardDamageTests.cs"
$compilerArgs+="$farmBuildDir\FarmCardForecast.cs";$compilerArgs+="$farmBuildDir\FarmCardForecastTests.cs"
$compilerArgs+="$farmBuildDir\FarmCardPiles.cs";$compilerArgs+="$farmBuildDir\FarmCardPileTests.cs"
$compilerArgs+="$farmBuildDir\FarmCardRewardFit.cs";$compilerArgs+="$farmBuildDir\FarmCardRewardFitTests.cs"
$compilerArgs+="$farmBuildDir\FarmCardCampTests.cs"
$compilerArgs+="$farmBuildDir\FarmCardBrowse.cs";$compilerArgs+="$farmBuildDir\FarmCardBrowseTests.cs"
$compilerArgs+="$farmBuildDir\FarmCardStatus.cs";$compilerArgs+="$farmBuildDir\FarmCardStatusTests.cs";$compilerArgs+="$farmBuildDir\FarmCardInspect.cs";$compilerArgs+="$farmBuildDir\FarmCardInspectTests.cs";$compilerArgs+="$farmBuildDir\FarmCardReceipt.cs";$compilerArgs+="$farmBuildDir\FarmCardReceiptTests.cs";$compilerArgs+="$farmBuildDir\FarmCardLesson.cs";$compilerArgs+="$farmBuildDir\FarmCardLessonTests.cs"
$responsePath=Join-Path $outputDir ($OutputName+'.rsp')
$compilerArgs | ForEach-Object { '"'+$_+'"' } | Set-Content -LiteralPath $responsePath -Encoding UTF8
& "$netDir\csc.exe" ("@"+$responsePath)
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}






& "$netDir\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll ("/out:"+(Join-Path $outputDir '卡牌试玩.exe')) (Join-Path $farmBuildDir 'GuideTestLauncher.cs')
if($LASTEXITCODE -ne 0){throw 'Launcher build failed.'}
"Built: $outputDir"
