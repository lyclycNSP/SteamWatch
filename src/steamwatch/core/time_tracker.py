"""
时长追踪模块
"""

import json
from pathlib import Path
from datetime import datetime, date
from typing import Dict, Optional, List
from dataclasses import dataclass, field, asdict
from threading import Lock


@dataclass
class DailyRecord:
    """每日游戏记录"""
    date: str  # YYYY-MM-DD
    game_playtimes: Dict[int, int] = field(default_factory=dict)  # app_id -> minutes
    
    def get_game_time(self, app_id: int) -> int:
        """获取指定游戏当日时长（分钟）"""
        return self.game_playtimes.get(app_id, 0)
    
    def get_total_time(self) -> int:
        """获取当日总时长（分钟）"""
        return sum(self.game_playtimes.values())


@dataclass
class GameTimeLimit:
    """游戏时长限制"""
    app_id: int
    daily_limit: int  # 每日限制（分钟），0表示无限制
    name: str = ""


class TimeTracker:
    """
    时长追踪器
    
    追踪每个游戏的游玩时长，提供时长限制和提醒功能
    """
    
    DATA_FILE = "data/timedata.json"
    CONFIG_FILE = "data/config.json"
    
    def __init__(self, data_dir: Optional[Path] = None):
        """
        初始化时长追踪器
        
        Args:
            data_dir: 数据目录路径
        """
        self.data_dir = data_dir or Path(".")
        self.data_dir.mkdir(parents=True, exist_ok=True)
        
        self._records: Dict[str, DailyRecord] = {}
        self._limits: Dict[int, GameTimeLimit] = {}
        self._global_limit: int = 0  # 全局每日限制（分钟），0表示无限制
        self._lock = Lock()
        
        self._load_data()
    
    def _load_data(self) -> None:
        """从文件加载数据"""
        data_file = self.data_dir / self.DATA_FILE
        if data_file.exists():
            try:
                with open(data_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    for date_str, record_data in data.get("records", {}).items():
                        self._records[date_str] = DailyRecord(
                            date=date_str,
                            game_playtimes={int(k): v for k, v in record_data.get("game_playtimes", {}).items()}
                        )
            except Exception as e:
                print(f"Error loading data: {e}")
        
        config_file = self.data_dir / self.CONFIG_FILE
        if config_file.exists():
            try:
                with open(config_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    self._global_limit = data.get("global_limit", 0)
                    for limit_data in data.get("limits", []):
                        limit = GameTimeLimit(**limit_data)
                        self._limits[limit.app_id] = limit
            except Exception as e:
                print(f"Error loading config: {e}")
    
    def _save_data(self) -> None:
        """保存数据到文件"""
        data_file = self.data_dir / self.DATA_FILE
        data_file.parent.mkdir(parents=True, exist_ok=True)
        
        with self._lock:
            try:
                records_data = {
                    date_str: {
                        "game_playtimes": record.game_playtimes
                    }
                    for date_str, record in self._records.items()
                }
                
                with open(data_file, "w", encoding="utf-8") as f:
                    json.dump({"records": records_data}, f, ensure_ascii=False, indent=2)
            except Exception as e:
                print(f"Error saving data: {e}")
    
    def _save_config(self) -> None:
        """保存配置到文件"""
        config_file = self.data_dir / self.CONFIG_FILE
        config_file.parent.mkdir(parents=True, exist_ok=True)
        
        with self._lock:
            try:
                config_data = {
                    "global_limit": self._global_limit,
                    "limits": [asdict(limit) for limit in self._limits.values()]
                }
                
                with open(config_file, "w", encoding="utf-8") as f:
                    json.dump(config_data, f, ensure_ascii=False, indent=2)
            except Exception as e:
                print(f"Error saving config: {e}")
    
    def _get_today(self) -> str:
        """获取今日日期字符串"""
        return date.today().isoformat()
    
    def _get_today_record(self) -> DailyRecord:
        """获取或创建今日记录"""
        today = self._get_today()
        if today not in self._records:
            self._records[today] = DailyRecord(date=today)
        return self._records[today]
    
    def add_game_time(self, app_id: int, minutes: int) -> None:
        """
        添加游戏时长
        
        Args:
            app_id: 游戏AppID
            minutes: 时长（分钟）
        """
        with self._lock:
            record = self._get_today_record()
            record.game_playtimes[app_id] = record.game_playtimes.get(app_id, 0) + minutes
        
        self._save_data()
    
    def set_game_time(self, app_id: int, minutes: int, day: Optional[str] = None) -> None:
        """
        设置游戏时长
        
        Args:
            app_id: 游戏AppID
            minutes: 时长（分钟）
            day: 日期，默认今天
        """
        day = day or self._get_today()
        
        with self._lock:
            if day not in self._records:
                self._records[day] = DailyRecord(date=day)
            self._records[day].game_playtimes[app_id] = minutes
        
        self._save_data()
    
    def get_game_time(self, app_id: int, day: Optional[str] = None) -> int:
        """
        获取游戏时长
        
        Args:
            app_id: 游戏AppID
            day: 日期，默认今天
            
        Returns:
            时长（分钟）
        """
        day = day or self._get_today()
        
        with self._lock:
            if day in self._records:
                return self._records[day].game_playtimes.get(app_id, 0)
        return 0
    
    def get_total_time(self, day: Optional[str] = None) -> int:
        """
        获取总时长
        
        Args:
            day: 日期，默认今天
            
        Returns:
            总时长（分钟）
        """
        day = day or self._get_today()
        
        with self._lock:
            if day in self._records:
                return self._records[day].get_total_time()
        return 0
    
    def get_recent_records(self, days: int = 7) -> List[DailyRecord]:
        """
        获取最近几天的记录
        
        Args:
            days: 天数
            
        Returns:
            记录列表
        """
        from datetime import timedelta
        
        records = []
        today = date.today()
        
        for i in range(days):
            day = (today - timedelta(days=i)).isoformat()
            if day in self._records:
                records.append(self._records[day])
            else:
                records.append(DailyRecord(date=day))
        
        return records
    
    def set_game_limit(self, app_id: int, daily_limit: int, name: str = "") -> None:
        """
        设置游戏时长限制
        
        Args:
            app_id: 游戏AppID
            daily_limit: 每日限制（分钟），0表示无限制
            name: 游戏名称
        """
        with self._lock:
            self._limits[app_id] = GameTimeLimit(
                app_id=app_id,
                daily_limit=daily_limit,
                name=name
            )
        
        self._save_config()
    
    def remove_game_limit(self, app_id: int) -> None:
        """
        移除游戏时长限制
        
        Args:
            app_id: 游戏AppID
        """
        with self._lock:
            if app_id in self._limits:
                del self._limits[app_id]
        
        self._save_config()
    
    def get_game_limit(self, app_id: int) -> Optional[GameTimeLimit]:
        """
        获取游戏时长限制
        
        Args:
            app_id: 游戏AppID
            
        Returns:
            时长限制，如果未设置则返回None
        """
        return self._limits.get(app_id)
    
    def set_global_limit(self, minutes: int) -> None:
        """
        设置全局每日时长限制
        
        Args:
            minutes: 限制（分钟），0表示无限制
        """
        with self._lock:
            self._global_limit = minutes
        
        self._save_config()
    
    def get_global_limit(self) -> int:
        """获取全局每日时长限制（分钟）"""
        return self._global_limit
    
    def check_limit(self, app_id: int) -> Dict[str, any]:
        """
        检查是否超出限制
        
        Args:
            app_id: 游戏AppID
            
        Returns:
            包含限制检查结果的字典
        """
        current_time = self.get_game_time(app_id)
        total_time = self.get_total_time()
        
        result = {
            "current_game_time": current_time,
            "total_time": total_time,
            "game_limit": None,
            "game_exceeded": False,
            "global_limit": self._global_limit,
            "global_exceeded": False,
        }
        
        if app_id in self._limits:
            limit = self._limits[app_id]
            result["game_limit"] = limit.daily_limit
            if limit.daily_limit > 0:
                result["game_exceeded"] = current_time >= limit.daily_limit
        
        if self._global_limit > 0:
            result["global_exceeded"] = total_time >= self._global_limit
        
        return result
    
    def check_approaching_limit(self, app_id: int, threshold: float = 0.8) -> Dict[str, any]:
        """
        检查是否接近限制
        
        Args:
            app_id: 游戏AppID
            threshold: 阈值比例（默认0.8，即80%）
            
        Returns:
            包含接近限制检查结果的字典
        """
        limit_check = self.check_limit(app_id)
        
        result = {
            "approaching_game_limit": False,
            "approaching_global_limit": False,
            "game_progress": 0.0,
            "global_progress": 0.0,
        }
        
        if limit_check["game_limit"] and limit_check["game_limit"] > 0:
            result["game_progress"] = limit_check["current_game_time"] / limit_check["game_limit"]
            result["approaching_game_limit"] = result["game_progress"] >= threshold
        
        if limit_check["global_limit"] > 0:
            result["global_progress"] = limit_check["total_time"] / limit_check["global_limit"]
            result["approaching_global_limit"] = result["global_progress"] >= threshold
        
        return result