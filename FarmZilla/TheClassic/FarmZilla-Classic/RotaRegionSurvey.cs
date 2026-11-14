using System;
using System.Drawing;
using System.Collections.Generic;
namespace RotaAvalon {
    public static class RegionSurvey {
        // A sweep samples what the server loads while traveling, not a global spawn list.
        // Sweeps fly at the character's current height: materials only load near the ground (owner, 02/11/2026).
        public static RouteDocument Plan(RectangleF visible,ClientSnapshot player,int maxPoints=600){
            if(player==null)throw new ArgumentException("Conecte ao personagem.");
            double left=Math.Max(-4096,visible.Left),right=Math.Min(4096,visible.Right),bottom=Math.Max(-5632,visible.Top),top=Math.Min(5632,visible.Bottom);
            if(!GameClient.ValidCoordinate(left)||!GameClient.ValidCoordinate(right)||!GameClient.ValidCoordinate(bottom)||!GameClient.ValidCoordinate(top)||right<=left||top<=bottom)throw new ArgumentException("Área inválida.");
            int columns=Math.Max(1,(int)Math.Ceiling((right-left)/120)),rows=Math.Max(1,(int)Math.Ceiling((top-bottom)/120));
            if((columns+1)*(long)(rows+1)>maxPoints)throw new ArgumentException("Área muito extensa para uma passagem. Aproxime o zoom e mapeie por trechos.");
            var points=new List<Waypoint>();
            // Start at the side nearest the character, then alternate direction each row.
            bool fromBottom=Math.Abs(player.Z-bottom)<Math.Abs(player.Z-top),fromLeft=Math.Abs(player.X-left)<Math.Abs(player.X-right);
            for(int row=0;row<=rows;row++){double z=fromBottom?bottom+(top-bottom)*row/rows:top-(top-bottom)*row/rows;bool forward=(row%2==0)==fromLeft;
                for(int column=0;column<=columns;column++){double x=forward?left+(right-left)*column/columns:right-(right-left)*column/columns;points.Add(new Waypoint{X=x,Y=player.Y,Z=z,Action="Passar"});}}
            return new RouteDocument{Name="Mapeamento da área",Character=player.Name,Loop=false,Points=points,FlightHeight=Math.Max(1,Math.Min(1000,player.Y/10))};
        }
    }
}
