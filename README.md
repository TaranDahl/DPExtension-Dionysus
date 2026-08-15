
# YRDynamicPatcher for 混乱工作室

[![license](https://www.gnu.org/graphics/gplv3-or-later.png)](https://www.gnu.org/licenses/gpl-3.0.en.html)

本项目基于[YRDynamicPatcher](https://github.com/Xkein/YRDynamicPatcher)，用于支持[航味麻酱](https://github.com/TaranDahl)的心灵终结扩展包[《心灵终结：混乱工作室》]()。

《心灵终结：混乱工作室》是一个以在《命令与征服：心灵终结》中实现《星际争霸2》中合作模式的[突变因子系统](https://starcraft.huijiwiki.com/wiki/%E5%90%88%E4%BD%9C%E4%BB%BB%E5%8A%A1/%E7%AA%81%E5%8F%98%E5%9B%A0%E5%AD%90)为目的的扩展包。

## For 玩家

请于《心灵终结：混乱工作室》的发布处获取完整打包。本项目不能独立运作。

## For 开发者

- 本项目之所以基于DP而不是Phobos编写，就是因为其中大部分功能完全没有通用性。

- 如果你确实想理解或修改本项目，那么你需要满足以下条件，难度从上到下递增：
    - 了解[突变因子系统](https://starcraft.huijiwiki.com/wiki/%E5%90%88%E4%BD%9C%E4%BB%BB%E5%8A%A1/%E7%AA%81%E5%8F%98%E5%9B%A0%E5%AD%90)
    - 熟悉红色警戒2的ini编写
    - 具有一定的编程能力
    - 熟悉[YRDynamicPatcher](https://github.com/Xkein/YRDynamicPatcher)
    - 具有足够的红色警戒2逆向工程知识，掌握红色警戒2代码的基本结构

- 本项目实际上并不依赖心灵终结。你可以较低的工作量将其部署到你的mod里，前提是你满足以上条件。

- 本项目基于YRDynamicPatcher修改而来，大部分新增代码位于[Mutators](Projects/Extension/Mutators)目录下。

## 待办

- 补全未实现的突变因子
    - 缺少代码的（混乱工作室，软、杀戮机器人等小地图图标）
    - 缺少肝的（其它）

- 实现突变因子：混乱工作室
    - 需要完善突变因子的UnInit函数，目前该函数尚未测试
    - 需要将所有ini实现的因子改为C#实现，需要WIC的Buff系统导出接口

- 解决游戏读档、自动进入下一关的问题时的问题
    - ScenarioExt存档读档失败

## 致谢

- 感谢[掉叶粽](https://github.com/Xkein)实现YRDynamicPatcher，并在本项目开发过程中提供了宝贵的帮助。
- 感谢[双杀步枪](https://github.com/ChrisLv-CN)在完善C#版本YRPP方面的大量工作。