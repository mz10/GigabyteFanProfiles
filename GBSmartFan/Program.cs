using System;
using Gigabyte.Engine.EnvironmentControl.CoolingDevice.Fan;
using Gigabyte.Engine.EnvironmentControl.HardwareMonitor;
using Gigabyte.EnvironmentControl.Common.CoolingDevice.Fan;
using Gigabyte.EnvironmentControl.Common.CoolingDevice.Fan.Depositary;
using Gigabyte.EnvironmentControl.Common.CoolingDevice.Fan.MultiCurveDepositary;
using Gigabyte.OxyPlotControls.Fan;
using System.Collections.Generic;
using ThermalConsole.Management;
using ThermalConsole.Utilities.Fan;

namespace GBSmartFan {
	static class Program {		
		public static void Main(string[] args) {
			if(args.Length == 0) {
				Console.WriteLine("Use gbsmartfan.exe profile.xml");
				Console.WriteLine("This program can load profile of fans on Gigabyte motherboards.");
				Console.WriteLine("");
				Console.WriteLine("Enter path to the xml file:");
				
				string file = Console.ReadLine();
				
				if(file != "") {
					new Fan(file);
				}
			}
			else if(args.Length >= 1) {
				Console.WriteLine("Loading profile...");
				new Fan(args[0]);
      }
		}
	}
		
	class Fan {
		SmartGuardianFanControlModule fanControlMgr = new SmartGuardianFanControlModule();
	  FanCalibrateDataManagement fanCalbDtMgr;
	  HardwareMonitorControlModule hwMonMgr = new HardwareMonitorControlModule();
	  FanTachometerTitleManagement fanTachMgr;
		SmartFanMultiCurveConfigProfileManagement fanConfigProfMgr;
		ControlHighAmperageFanHeaderManagement highAmpFanMgr = new ControlHighAmperageFanHeaderManagement();
		ControlAutoFanStopManagement autoFanStopMgr = new ControlAutoFanStopManagement();
		List<SmartFanMultiCurveConfig> pNewSmartFanConfigs = new List<SmartFanMultiCurveConfig>();
		SmartFanPlotModel ucSmartFanPlotModel;
		
		public Fan(string pathXML) {	
			this.fanConfigProfMgr = new SmartFanMultiCurveConfigProfileManagement(this.fanControlMgr.FanControlCount);
			
			bool profHasSameAtr;
			bool res = this.SyncSmartFanProfile(pathXML, ref pNewSmartFanConfigs, out profHasSameAtr);
			
			if(res) {
				this.SetupFanControl(pNewSmartFanConfigs);
			}
		}	
		
		
    void SetupFanControl(List<SmartFanMultiCurveConfig> newSmartFanConfigs) {      
      if (newSmartFanConfigs == null || newSmartFanConfigs.Count == 0 || this.hwMonMgr == null)
        return;
      
      if (this.fanControlMgr == null)
        return;
      
      try {
        for (int fanControlIndex = 0; fanControlIndex < newSmartFanConfigs.Count; ++fanControlIndex) {
          var newCfg = newSmartFanConfigs[fanControlIndex];
          
          if (newCfg.Mode == FanConfigMode.SmartFanMode) {
            var smartModeConfig = newCfg.SmartModeConfig;
            
            if (newCfg.SoftwareOperationMode) {
              int slopeIndex = SmartFanControlConfigConverter.IndexOf(Convert.ToSingle(this.ucSmartFanPlotModel.TemperatureValue), smartModeConfig);
             
              if (slopeIndex < 0) slopeIndex = 0;
              
              this.fanControlMgr.Set(fanControlIndex, slopeIndex, smartModeConfig);
            }
            else
              this.fanControlMgr.Set(fanControlIndex, smartModeConfig);
            
          }
          else {
            SmartFanControlConfig fixedModeConfig = newCfg.FixedModeConfig;
            this.fanControlMgr.Set(fanControlIndex, fixedModeConfig);
          }
        }
      }
      catch
      {
        throw;
      }
    }
	
		   
    bool SyncSmartFanProfile(string xmlFilePath, ref List<SmartFanMultiCurveConfig> pNewFanCfgs, out bool profHasSameAtr) {
      bool pbTargetConfigChanged = false;
      bool pbSameAttribute = false;
      
      profHasSameAtr = false;
      
      if (pNewFanCfgs == null)
      	return false;
      
      if (pNewFanCfgs.Count > 0)
        pNewFanCfgs.Clear();
      
      if (string.IsNullOrEmpty(xmlFilePath) || string.IsNullOrWhiteSpace(xmlFilePath) || (this.fanConfigProfMgr == null || this.fanConfigProfMgr.FanControlCount == 0)) {
      	return false;
      }
      
      int fanControlCount = this.fanConfigProfMgr.FanControlCount;
      
      for (int fanControlIndex = 0; fanControlIndex < fanControlCount; ++fanControlIndex) {
        var curveCfg = new SmartFanMultiCurveConfig();
        this.fanConfigProfMgr.Read(fanControlIndex, ref curveCfg);
        pNewFanCfgs.Add(curveCfg);
      }
      
      if (pNewFanCfgs.Count == 0)
      	return false;
      
      try {
        var exportMgr = new SmartFanMultiCurveConfigProfileImportExportManagement();
        bool readed = exportMgr.ReadFile(xmlFilePath, true, ref pNewFanCfgs, out pbSameAttribute);
        profHasSameAtr = pbSameAttribute;
        
        if (!readed || pNewSmartFanConfigs.Count == 0) {
       	  Console.WriteLine("Invalid file: " + xmlFilePath);
       	  return false;
        }
        
        SyncSmarFanProfile4HighAmperageFanHeader(true, ref pNewFanCfgs, out pbTargetConfigChanged);
        
        for (int fanControlIndex = 0; fanControlIndex < pNewFanCfgs.Count; ++fanControlIndex) {
          SmartFanMultiCurveConfig newSmartFanMultiCurveConfig = pNewFanCfgs[fanControlIndex];
          this.fanConfigProfMgr.Write(fanControlIndex, newSmartFanMultiCurveConfig);
        }
      }
      catch(Exception ex) {
      	Console.WriteLine("Exception: " + ex.Message);
      	return false;
      }
      
      return true;
    }	


    bool CheckSupported4AutoFanStopMode() {
      return !InitAutoFanStopManagementObject((ControlAutoFanStopManagement) null) || this.autoFanStopMgr == null ? false : this.autoFanStopMgr.IsSupported;
    }	   
    
    bool InitAutoFanStopManagementObject(ControlAutoFanStopManagement fanModeMgr) {
      try {
        if (fanModeMgr != null) {
          this.autoFanStopMgr = fanModeMgr;
        }
        else if (this.autoFanStopMgr == null)
          this.autoFanStopMgr = new ControlAutoFanStopManagement();
        	return true;
      }
      catch {
        return false;
      }
    }
		  

    bool RetrieveSupportAutoFanStopStatus(ref List<bool> pFanStopStatus) {
      int pMinimumDutyCyclePercetage = 0;
      
      if (pFanStopStatus == null)
      	return false;
      
      if (pFanStopStatus.Count > 0)
        pFanStopStatus.Clear();
      
      bool flag2 = InitAutoFanStopManagementObject((ControlAutoFanStopManagement) null);
      
      if (!flag2 || this.autoFanStopMgr == null || !this.autoFanStopMgr.IsSupported)
        return flag2;
      
      int fanControlCount = this.fanControlMgr.FanControlCount;
      
      if (fanControlCount == 0)
        return flag2;
      
      for (int fanControlIndex = 0; fanControlIndex < fanControlCount; ++fanControlIndex) {
        bool flag3 = CheckEnableProtection4HighAmperageFanHeader(fanControlIndex, out pMinimumDutyCyclePercetage) || CheckFanCalibrationDataExist(fanControlIndex);
        pFanStopStatus.Add(flag3);
      }
      
      return pFanStopStatus.Count > 0;
    }

    
    bool SyncSmarFanProfile4HighAmperageFanHeader(bool bUseOrigConfig, ref List<SmartFanMultiCurveConfig> targetCfgs, out bool pbTargetConfigChanged) {
      int pFanControlIndex = -1;
      int pMinDutyCyclePercetage = 0;
      pbTargetConfigChanged = false;
      
      if (targetCfgs == null || targetCfgs.Count == 0)
      	return false;
      
      if (!this.CheckAnyHighAmperageFanHeaderEnable(out pFanControlIndex, out pMinDutyCyclePercetage) || pFanControlIndex == -1)
        return true;
      
      if (pFanControlIndex >= targetCfgs.Count)
      	return false;
      
      if (this.highAmpFanMgr.IsQualify(targetCfgs[pFanControlIndex]))
        return true;
      
      bool ret;
      
      if (bUseOrigConfig) {
        var mCfg = new SmartFanMultiCurveConfig();
        this.fanConfigProfMgr.Read(pFanControlIndex, ref mCfg);
        targetCfgs.RemoveAt(pFanControlIndex);
        targetCfgs.Insert(pFanControlIndex, mCfg);
        pbTargetConfigChanged = true;
        ret = true;
      }
      else {
        SmartFanMultiCurveConfig targetSmartFanMultiCurveConfig = targetCfgs[pFanControlIndex];
        ret = this.highAmpFanMgr.ModifyProfile(pFanControlIndex, SmartFanLevels.Standard, ref targetSmartFanMultiCurveConfig, out pbTargetConfigChanged);
      }
      
      return ret;
    }

    
    bool CheckEnableProtection4HighAmperageFanHeader(int fanControlIndex, out int pMinimumDutyCyclePercetage) {
      string pFanControlTitle = string.Empty;
      pMinimumDutyCyclePercetage = 0;
      
      if (fanControlIndex < 0 || !InitHighAmperageFanHeaderManagementObject((ControlHighAmperageFanHeaderManagement) null) || (this.highAmpFanMgr == null || !this.highAmpFanMgr.IsSupported) || (this.fanTachMgr == null || !this.fanTachMgr.IsSupported || !this.fanTachMgr.GetFanControlTitle(fanControlIndex, out pFanControlTitle)) || (string.IsNullOrEmpty(pFanControlTitle) && string.IsNullOrWhiteSpace(pFanControlTitle) || (!this.highAmpFanMgr.IsHighAmperageFanTitle(pFanControlTitle) || !this.highAmpFanMgr.IsEnable())))
      	return false;

      pMinimumDutyCyclePercetage = this.highAmpFanMgr.MinimumDutyCyclePercentage;
      
      return true;
    }
    
    
    bool InitHighAmperageFanHeaderManagementObject(ControlHighAmperageFanHeaderManagement oHighAmpFanMgr) {
      try {
        if (oHighAmpFanMgr != null)
          this.highAmpFanMgr = oHighAmpFanMgr;
        else if (this.highAmpFanMgr == null)
          this.highAmpFanMgr = new ControlHighAmperageFanHeaderManagement();
        
        return true;
      }
      catch
      {
        return false;
      }
    }
    
    bool CheckFanCalibrationDataExist(int fanControlIndex) {
      return this.fanCalbDtMgr == null ? false : this.fanCalbDtMgr.IsExist(fanControlIndex);
    }    
        
    bool CheckAnyHighAmperageFanHeaderEnable(out int pFanControlIndex, out int pMinimumDutyCyclePercetage) {
      bool ret = false;
      int pMinimumDutyCyclePercetage1 = 0;
      pFanControlIndex = -1;
      pMinimumDutyCyclePercetage = 0;
      
      if (this.fanControlMgr == null || this.fanControlMgr.FanControlCount == 0)
      	return false;
      
      int fanControlCount = this.fanControlMgr.FanControlCount;
      
      for (int fanControlIndex = 0; fanControlIndex < fanControlCount; ++fanControlIndex) {
        if (CheckEnableProtection4HighAmperageFanHeader(fanControlIndex, out pMinimumDutyCyclePercetage1)) {
          pFanControlIndex = fanControlIndex;
          pMinimumDutyCyclePercetage = pMinimumDutyCyclePercetage1;
          ret = true;
          break;
        }
      }
      
      return ret;
    }	
	}
}